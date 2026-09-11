use base64::Engine as _;
use citra_capsules::{Capsule, CapsuleManager};
use citra_machine::{MachineClient, MachineHealth};
use citra_network::{NetworkBroker, NetworkError, RequestPurpose};
use citra_protocol::{
    MachineDecision, MachineResponse, PageContext, VisualResource, VisualResourceEvent,
    MACHINE_PROTOCOL_VERSION,
};
use citra_renderer::{parse_document, start_document, ImageRequest, RenderDocument};
use sha2::{Digest, Sha256};
use std::collections::HashMap;
use std::fmt;
use std::io::{Read, Write};
use std::path::{Path, PathBuf};
use std::process::{Command, Stdio};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{mpsc::Sender, Arc, Mutex};
use std::thread;
use std::time::{SystemTime, UNIX_EPOCH};
use url::Url;

const MAX_IMAGES_PER_DOCUMENT: usize = 32;
const MAX_DECISION_CACHE: usize = 512;
static NAV_COUNTER: AtomicU64 = AtomicU64::new(1);

#[derive(Debug, Clone)]
pub enum CoreEvent {
    NavigationStarted {
        tab_id: u64,
        navigation_id: u64,
        requested_url: String,
    },
    DocumentReady {
        tab_id: u64,
        navigation_id: u64,
        document: RenderDocument,
        status: String,
    },
    ImageReady {
        tab_id: u64,
        navigation_id: u64,
        resource_id: String,
        image: DisplayImage,
        machine_reason: String,
    },
    ImageBlocked {
        tab_id: u64,
        navigation_id: u64,
        resource_id: String,
        reason: String,
    },
    NavigationFailed {
        tab_id: u64,
        navigation_id: u64,
        message: String,
    },
}

#[derive(Debug, Clone)]
pub struct DisplayImage {
    pub width: u32,
    pub height: u32,
    pub rgba: Vec<u8>,
}

#[derive(Debug)]
pub enum CoreError {
    Network(NetworkError),
    InvalidUrl(String),
    Decoder(String),
}

impl fmt::Display for CoreError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Network(e) => write!(f, "{e}"),
            Self::InvalidUrl(e) => write!(f, "{e}"),
            Self::Decoder(e) => write!(f, "image decoder: {e}"),
        }
    }
}

impl std::error::Error for CoreError {}

impl From<NetworkError> for CoreError {
    fn from(value: NetworkError) -> Self {
        Self::Network(value)
    }
}

#[derive(Debug)]
pub struct CitraCore {
    session_id: String,
    network: NetworkBroker,
    machine: Arc<MachineClient>,
    capsules: CapsuleManager,
    decoder_path: PathBuf,
    decision_cache: Mutex<HashMap<String, MachineResponse>>,
}

impl CitraCore {
    pub fn new() -> Result<Arc<Self>, CoreError> {
        let decoder_path = decoder_path();
        Ok(Arc::new(Self {
            session_id: new_session_id(),
            network: NetworkBroker::new()?,
            machine: Arc::new(MachineClient::default()),
            capsules: CapsuleManager,
            decoder_path,
            decision_cache: Mutex::new(HashMap::new()),
        }))
    }

    pub fn new_anonymous_capsule(&self) -> Capsule {
        self.capsules.new_anonymous()
    }

    pub fn machine_health(&self) -> MachineHealth {
        self.machine.health()
    }

    pub fn dev_allow_visuals(&self) -> bool {
        self.machine.dev_allow_visuals()
    }

    pub fn set_dev_allow_visuals(&self, enabled: bool) {
        self.machine.set_dev_allow_visuals(enabled);
    }

    pub fn next_navigation_id(&self) -> u64 {
        NAV_COUNTER.fetch_add(1, Ordering::Relaxed)
    }

    pub fn start_document(&self) -> RenderDocument {
        start_document()
    }

    pub fn resolve_input(&self, input: &str) -> Result<Url, CoreError> {
        let input = input.trim();
        if input.is_empty() {
            return Err(CoreError::InvalidUrl("empty location".into()));
        }

        if let Ok(url) = Url::parse(input) {
            if matches!(url.scheme(), "http" | "https") {
                return Ok(url);
            }
        }

        if !input.contains(char::is_whitespace) && input.contains('.') {
            return Url::parse(&format!("https://{input}"))
                .map_err(|e| CoreError::InvalidUrl(e.to_string()));
        }

        let mut serializer = url::form_urlencoded::Serializer::new(String::new());
        serializer.append_pair("q", input);
        let query = serializer.finish();
        Url::parse(&format!("https://html.duckduckgo.com/html/?{query}"))
            .map_err(|e| CoreError::InvalidUrl(e.to_string()))
    }

    pub fn spawn_navigation(
        self: &Arc<Self>,
        tab_id: u64,
        navigation_id: u64,
        capsule: Capsule,
        input: String,
        tx: Sender<CoreEvent>,
    ) {
        let core = Arc::clone(self);
        thread::spawn(move || {
            if let Err(error) = core.navigate_worker(tab_id, navigation_id, capsule, input, &tx) {
                let _ = tx.send(CoreEvent::NavigationFailed {
                    tab_id,
                    navigation_id,
                    message: error.to_string(),
                });
            }
        });
    }

    fn navigate_worker(
        &self,
        tab_id: u64,
        navigation_id: u64,
        capsule: Capsule,
        input: String,
        tx: &Sender<CoreEvent>,
    ) -> Result<(), CoreError> {
        let url = self.resolve_input(&input)?;
        let _ = tx.send(CoreEvent::NavigationStarted {
            tab_id,
            navigation_id,
            requested_url: url.to_string(),
        });

        let response = self.network.fetch(url, RequestPurpose::Navigation)?;
        let final_url = response.final_url.clone();
        let html = String::from_utf8_lossy(&response.bytes);
        let document = parse_document(&html, &final_url);
        let images = document
            .images
            .iter()
            .take(MAX_IMAGES_PER_DOCUMENT)
            .cloned()
            .collect::<Vec<_>>();
        let _ = tx.send(CoreEvent::DocumentReady {
            tab_id,
            navigation_id,
            document,
            status: format!("HTTP {} · {}", response.status, response.mime),
        });

        for image in images {
            match self.process_image(tab_id, navigation_id, &capsule, &final_url, &image) {
                Ok(ImageOutcome::ReadyWithId { resource_id, image, reason }) => {
                    let _ = tx.send(CoreEvent::ImageReady {
                        tab_id,
                        navigation_id,
                        resource_id,
                        image,
                        machine_reason: reason,
                    });
                }
                Ok(ImageOutcome::Blocked { resource_id, reason }) => {
                    let _ = tx.send(CoreEvent::ImageBlocked {
                        tab_id,
                        navigation_id,
                        resource_id,
                        reason,
                    });
                }
                Err(error) => {
                    let _ = tx.send(CoreEvent::ImageBlocked {
                        tab_id,
                        navigation_id,
                        resource_id: image.resource_id.clone(),
                        reason: error.to_string(),
                    });
                }
            }
        }
        Ok(())
    }

    fn process_image(
        &self,
        tab_id: u64,
        navigation_id: u64,
        capsule: &Capsule,
        page_url: &Url,
        request: &ImageRequest,
    ) -> Result<ImageOutcome, CoreError> {
        let url = Url::parse(&request.url).map_err(|e| CoreError::InvalidUrl(e.to_string()))?;
        let response = self.network.fetch(url, RequestPurpose::Image)?;
        let decoded = decode_image(&self.decoder_path, &response.bytes)?;
        let sha256 = sha256_hex(&response.bytes);

        let cached = self
            .decision_cache
            .lock()
            .ok()
            .and_then(|cache| cache.get(&sha256).cloned());

        let decision = if let Some(cached) = cached {
            cached
        } else {
            let event = VisualResourceEvent {
                event: "visual.resource".into(),
                version: MACHINE_PROTOCOL_VERSION,
                session_id: self.session_id.clone(),
                capsule_id: capsule.id.clone(),
                tab_id,
                navigation_id,
                resource_id: request.resource_id.clone(),
                timestamp_utc_ms: now_ms(),
                page: PageContext {
                    url_hash: sha256_hex(page_url.as_str().as_bytes()),
                    domain: page_url.host_str().unwrap_or_default().to_string(),
                },
                resource: VisualResource {
                    url_hash: sha256_hex(response.final_url.as_str().as_bytes()),
                    mime: response.mime,
                    byte_len: response.bytes.len(),
                    width: decoded.display.width,
                    height: decoded.display.height,
                    sha256: sha256.clone(),
                    preview_width: decoded.preview_width,
                    preview_height: decoded.preview_height,
                    preview_rgba_base64: base64::engine::general_purpose::STANDARD
                        .encode(&decoded.preview_rgba),
                },
            };
            let decision = self.machine.decide_visual(&event);
            if let Ok(mut cache) = self.decision_cache.lock() {
                if cache.len() >= MAX_DECISION_CACHE {
                    cache.clear();
                }
                cache.insert(sha256, decision.clone());
            }
            decision
        };

        let reason = decision.reason.clone().unwrap_or_else(|| format!("{:?}", decision.decision));
        match decision.decision {
            MachineDecision::Allow => Ok(ImageOutcome::ReadyWithId {
                resource_id: request.resource_id.clone(),
                image: decoded.display,
                reason,
            }),
            MachineDecision::Block => Ok(ImageOutcome::Blocked {
                resource_id: request.resource_id.clone(),
                reason,
            }),
            MachineDecision::Blur
            | MachineDecision::Replace
            | MachineDecision::Defer
            | MachineDecision::ScanMore => Ok(ImageOutcome::Blocked {
                resource_id: request.resource_id.clone(),
                reason: format!("{:?} is quarantined in Citra 0.1: {reason}", decision.decision),
            }),
        }
    }
}

#[derive(Debug)]
enum ImageOutcome {
    ReadyWithId { resource_id: String, image: DisplayImage, reason: String },
    Blocked { resource_id: String, reason: String },
}

#[derive(Debug)]
struct DecodedImage {
    display: DisplayImage,
    preview_width: u32,
    preview_height: u32,
    preview_rgba: Vec<u8>,
}

fn decode_image(decoder_path: &Path, bytes: &[u8]) -> Result<DecodedImage, CoreError> {
    let mut child = Command::new(decoder_path)
        .stdin(Stdio::piped())
        .stdout(Stdio::piped())
        .stderr(Stdio::piped())
        .spawn()
        .map_err(|e| CoreError::Decoder(format!("cannot start {}: {e}", decoder_path.display())))?;

    {
        let mut stdin = child
            .stdin
            .take()
            .ok_or_else(|| CoreError::Decoder("decoder stdin unavailable".into()))?;
        stdin
            .write_all(bytes)
            .map_err(|e| CoreError::Decoder(e.to_string()))?;
    }

    let output = child
        .wait_with_output()
        .map_err(|e| CoreError::Decoder(e.to_string()))?;
    if !output.status.success() {
        return Err(CoreError::Decoder(
            String::from_utf8_lossy(&output.stderr).trim().to_string(),
        ));
    }

    parse_decoder_output(&output.stdout)
}

fn parse_decoder_output(bytes: &[u8]) -> Result<DecodedImage, CoreError> {
    let mut cursor = std::io::Cursor::new(bytes);
    let mut magic = [0u8; 4];
    cursor.read_exact(&mut magic).map_err(decoder_io)?;
    if &magic != b"CIMG" {
        return Err(CoreError::Decoder("bad decoder protocol magic".into()));
    }
    let version = read_u32(&mut cursor)?;
    if version != 1 {
        return Err(CoreError::Decoder(format!("unsupported decoder protocol {version}")));
    }
    let display_width = read_u32(&mut cursor)?;
    let display_height = read_u32(&mut cursor)?;
    let preview_width = read_u32(&mut cursor)?;
    let preview_height = read_u32(&mut cursor)?;
    let display_len = read_u64(&mut cursor)? as usize;
    let preview_len = read_u64(&mut cursor)? as usize;

    let expected_display = (display_width as usize)
        .checked_mul(display_height as usize)
        .and_then(|n| n.checked_mul(4))
        .ok_or_else(|| CoreError::Decoder("display dimensions overflow".into()))?;
    let expected_preview = (preview_width as usize)
        .checked_mul(preview_height as usize)
        .and_then(|n| n.checked_mul(4))
        .ok_or_else(|| CoreError::Decoder("preview dimensions overflow".into()))?;
    if display_len != expected_display || preview_len != expected_preview {
        return Err(CoreError::Decoder("decoder length mismatch".into()));
    }

    let mut display = vec![0u8; display_len];
    cursor.read_exact(&mut display).map_err(decoder_io)?;
    let mut preview = vec![0u8; preview_len];
    cursor.read_exact(&mut preview).map_err(decoder_io)?;

    Ok(DecodedImage {
        display: DisplayImage {
            width: display_width,
            height: display_height,
            rgba: display,
        },
        preview_width,
        preview_height,
        preview_rgba: preview,
    })
}

fn decoder_io(error: std::io::Error) -> CoreError {
    CoreError::Decoder(error.to_string())
}

fn read_u32(reader: &mut impl Read) -> Result<u32, CoreError> {
    let mut bytes = [0u8; 4];
    reader.read_exact(&mut bytes).map_err(decoder_io)?;
    Ok(u32::from_le_bytes(bytes))
}

fn read_u64(reader: &mut impl Read) -> Result<u64, CoreError> {
    let mut bytes = [0u8; 8];
    reader.read_exact(&mut bytes).map_err(decoder_io)?;
    Ok(u64::from_le_bytes(bytes))
}

fn decoder_path() -> PathBuf {
    let exe = std::env::current_exe().unwrap_or_else(|_| PathBuf::from("Citra.exe"));
    let dir = exe.parent().unwrap_or_else(|| Path::new("."));
    #[cfg(windows)]
    let name = "citra-image-decoder.exe";
    #[cfg(not(windows))]
    let name = "citra-image-decoder";
    dir.join(name)
}

fn sha256_hex(bytes: &[u8]) -> String {
    let digest = Sha256::digest(bytes);
    let mut out = String::with_capacity(64);
    for b in digest {
        use std::fmt::Write as _;
        let _ = write!(&mut out, "{b:02x}");
    }
    out
}

fn now_ms() -> u128 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_millis()
}

fn new_session_id() -> String {
    format!("session-{:x}-{:x}", now_ms(), std::process::id())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn search_uses_html_duckduckgo() {
        let core = CitraCore::new();
        if let Ok(core) = core {
            let url = core.resolve_input("lightweight browsers").unwrap();
            assert_eq!(url.host_str(), Some("html.duckduckgo.com"));
            assert!(url.query().unwrap_or_default().contains("q=lightweight+browsers"));
        }
    }
}
