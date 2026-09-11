use citra_protocol::{MachineResponse, VisualResourceEvent};
use std::fmt;
use std::sync::atomic::{AtomicBool, AtomicU8, Ordering};

const STATE_UNKNOWN: u8 = 0;
const STATE_ONLINE: u8 = 1;
const STATE_OFFLINE_PROTECTED: u8 = 2;
const STATE_DEV_ALLOW: u8 = 3;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MachineHealth {
    Unknown,
    Online,
    OfflineProtected,
    OfflineDevAllow,
}

impl MachineHealth {
    pub fn label(self) -> &'static str {
        match self {
            Self::Unknown => "UNKNOWN",
            Self::Online => "ONLINE",
            Self::OfflineProtected => "OFFLINE / VISUALS QUARANTINED",
            Self::OfflineDevAllow => "OFFLINE / DEV ALLOW",
        }
    }
}

#[derive(Debug)]
pub struct MachineError(pub String);

impl fmt::Display for MachineError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "{}", self.0)
    }
}

impl std::error::Error for MachineError {}

pub trait MachineBridge: Send + Sync {
    fn decide_visual(&self, event: &VisualResourceEvent) -> Result<MachineResponse, MachineError>;
}

#[derive(Debug, Clone)]
pub struct NamedPipeMachineBridge {
    pipe_path: String,
}

impl Default for NamedPipeMachineBridge {
    fn default() -> Self {
        Self::new(r"\\.\pipe\MiniMachineBrain")
    }
}

impl NamedPipeMachineBridge {
    pub fn new(pipe_path: impl Into<String>) -> Self {
        Self {
            pipe_path: pipe_path.into(),
        }
    }
}

#[cfg(windows)]
impl MachineBridge for NamedPipeMachineBridge {
    fn decide_visual(&self, event: &VisualResourceEvent) -> Result<MachineResponse, MachineError> {
        use std::fs::OpenOptions;
        use std::io::{BufRead, BufReader, BufWriter, Write};

        let pipe = OpenOptions::new()
            .read(true)
            .write(true)
            .open(&self.pipe_path)
            .map_err(|e| MachineError(format!("MiniMachine pipe unavailable: {e}")))?;

        let reader_pipe = pipe
            .try_clone()
            .map_err(|e| MachineError(format!("Cannot clone MiniMachine pipe: {e}")))?;

        let mut writer = BufWriter::new(pipe);
        serde_json::to_writer(&mut writer, event)
            .map_err(|e| MachineError(format!("Cannot serialize Machine event: {e}")))?;
        writer
            .write_all(b"\n")
            .and_then(|_| writer.flush())
            .map_err(|e| MachineError(format!("Cannot write Machine event: {e}")))?;

        let mut reader = BufReader::new(reader_pipe);
        let mut line = String::new();
        reader
            .read_line(&mut line)
            .map_err(|e| MachineError(format!("Cannot read Machine decision: {e}")))?;

        if line.trim().is_empty() {
            return Err(MachineError("MiniMachine returned an empty response".into()));
        }

        serde_json::from_str::<MachineResponse>(&line)
            .map_err(|e| MachineError(format!("Invalid MiniMachine response: {e}")))
    }
}

#[cfg(not(windows))]
impl MachineBridge for NamedPipeMachineBridge {
    fn decide_visual(&self, _event: &VisualResourceEvent) -> Result<MachineResponse, MachineError> {
        Err(MachineError(
            "MiniMachine named pipe is only implemented on Windows in Citra 0.1".into(),
        ))
    }
}

pub struct MachineClient {
    bridge: Box<dyn MachineBridge>,
    dev_allow_visuals: AtomicBool,
    state: AtomicU8,
}

impl fmt::Debug for MachineClient {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("MachineClient")
            .field("dev_allow_visuals", &self.dev_allow_visuals.load(Ordering::Relaxed))
            .field("health", &self.health())
            .finish()
    }
}

impl Default for MachineClient {
    fn default() -> Self {
        let dev_allow = std::env::var("CITRA_DEV_ALLOW_VISUALS")
            .map(|v| matches!(v.as_str(), "1" | "true" | "TRUE" | "yes" | "YES"))
            .unwrap_or(false);

        Self {
            bridge: Box::new(NamedPipeMachineBridge::default()),
            dev_allow_visuals: AtomicBool::new(dev_allow),
            state: AtomicU8::new(STATE_UNKNOWN),
        }
    }
}

impl MachineClient {
    pub fn with_bridge(bridge: Box<dyn MachineBridge>) -> Self {
        Self {
            bridge,
            dev_allow_visuals: AtomicBool::new(false),
            state: AtomicU8::new(STATE_UNKNOWN),
        }
    }

    pub fn set_dev_allow_visuals(&self, enabled: bool) {
        self.dev_allow_visuals.store(enabled, Ordering::Relaxed);
        if enabled && self.health() != MachineHealth::Online {
            self.state.store(STATE_DEV_ALLOW, Ordering::Relaxed);
        } else if !enabled && self.health() == MachineHealth::OfflineDevAllow {
            self.state.store(STATE_OFFLINE_PROTECTED, Ordering::Relaxed);
        }
    }

    pub fn dev_allow_visuals(&self) -> bool {
        self.dev_allow_visuals.load(Ordering::Relaxed)
    }

    pub fn health(&self) -> MachineHealth {
        match self.state.load(Ordering::Relaxed) {
            STATE_ONLINE => MachineHealth::Online,
            STATE_OFFLINE_PROTECTED => MachineHealth::OfflineProtected,
            STATE_DEV_ALLOW => MachineHealth::OfflineDevAllow,
            _ => MachineHealth::Unknown,
        }
    }

    pub fn decide_visual(&self, event: &VisualResourceEvent) -> MachineResponse {
        match self.bridge.decide_visual(event) {
            Ok(response) => {
                self.state.store(STATE_ONLINE, Ordering::Relaxed);
                response
            }
            Err(error) if self.dev_allow_visuals() => {
                self.state.store(STATE_DEV_ALLOW, Ordering::Relaxed);
                MachineResponse::allow(
                    &event.resource_id,
                    format!("DEV ALLOW: MiniMachine offline ({error})"),
                )
            }
            Err(error) => {
                self.state
                    .store(STATE_OFFLINE_PROTECTED, Ordering::Relaxed);
                MachineResponse::defer(
                    &event.resource_id,
                    format!("Protected fail-closed: MiniMachine offline ({error})"),
                )
            }
        }
    }
}
