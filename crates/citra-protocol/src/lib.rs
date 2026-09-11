use serde::{Deserialize, Serialize};

pub const MACHINE_PROTOCOL_VERSION: u32 = 1;

#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "SCREAMING_SNAKE_CASE")]
pub enum MachineDecision {
    Allow,
    Block,
    Blur,
    Replace,
    Defer,
    ScanMore,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct VisualResourceEvent {
    pub event: String,
    pub version: u32,
    pub session_id: String,
    pub capsule_id: String,
    pub tab_id: u64,
    pub navigation_id: u64,
    pub resource_id: String,
    pub timestamp_utc_ms: u128,
    pub page: PageContext,
    pub resource: VisualResource,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct PageContext {
    pub url_hash: String,
    pub domain: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct VisualResource {
    pub url_hash: String,
    pub mime: String,
    pub byte_len: usize,
    pub width: u32,
    pub height: u32,
    pub sha256: String,
    pub preview_width: u32,
    pub preview_height: u32,
    /// Raw RGBA8 preview, base64 encoded. This is intentionally small and bounded.
    pub preview_rgba_base64: String,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MachineResponse {
    pub resource_id: String,
    pub decision: MachineDecision,
    #[serde(default)]
    pub risk: Option<f32>,
    #[serde(default)]
    pub confidence: Option<f32>,
    #[serde(default)]
    pub cache_for_seconds: Option<u64>,
    #[serde(default)]
    pub reason: Option<String>,
}

impl MachineResponse {
    pub fn allow(resource_id: impl Into<String>, reason: impl Into<String>) -> Self {
        Self {
            resource_id: resource_id.into(),
            decision: MachineDecision::Allow,
            risk: None,
            confidence: None,
            cache_for_seconds: None,
            reason: Some(reason.into()),
        }
    }

    pub fn defer(resource_id: impl Into<String>, reason: impl Into<String>) -> Self {
        Self {
            resource_id: resource_id.into(),
            decision: MachineDecision::Defer,
            risk: None,
            confidence: None,
            cache_for_seconds: None,
            reason: Some(reason.into()),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn machine_decisions_are_uppercase_on_wire() {
        let response = MachineResponse::allow("img-1", "test");
        let json = serde_json::to_string(&response).unwrap();
        assert!(json.contains("\"ALLOW\""));
    }
}
