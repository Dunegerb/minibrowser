use std::sync::atomic::{AtomicU64, Ordering};
use std::time::{SystemTime, UNIX_EPOCH};

static CAPSULE_COUNTER: AtomicU64 = AtomicU64::new(1);

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum CapsuleKind {
    Anonymous,
}

#[derive(Debug, Clone)]
pub struct Capsule {
    pub id: String,
    pub name: String,
    pub kind: CapsuleKind,
    pub ephemeral: bool,
}

#[derive(Debug, Default)]
pub struct CapsuleManager;

impl CapsuleManager {
    pub fn new_anonymous(&self) -> Capsule {
        let now = SystemTime::now()
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_millis();
        let n = CAPSULE_COUNTER.fetch_add(1, Ordering::Relaxed);
        Capsule {
            id: format!("anon-{now:x}-{n:x}"),
            name: "Anonymous".into(),
            kind: CapsuleKind::Anonymous,
            ephemeral: true,
        }
    }
}
