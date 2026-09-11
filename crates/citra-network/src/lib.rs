use reqwest::blocking::{Client, Response};
use reqwest::header::{ACCEPT, ACCEPT_LANGUAGE, LOCATION, USER_AGENT};
use reqwest::redirect::Policy;
use std::fmt;
use std::io::Read;
use std::time::Duration;
use url::Url;

pub const MAX_DOCUMENT_BYTES: usize = 4 * 1024 * 1024;
pub const MAX_IMAGE_BYTES: usize = 8 * 1024 * 1024;
pub const MAX_REDIRECTS: usize = 8;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum RequestPurpose {
    Navigation,
    Image,
}

#[derive(Debug)]
pub enum NetworkError {
    Policy(String),
    Transport(String),
    TooLarge { limit: usize },
    Redirect(String),
}

impl fmt::Display for NetworkError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            Self::Policy(s) => write!(f, "network policy: {s}"),
            Self::Transport(s) => write!(f, "network transport: {s}"),
            Self::TooLarge { limit } => write!(f, "resource exceeds {} bytes", limit),
            Self::Redirect(s) => write!(f, "redirect error: {s}"),
        }
    }
}

impl std::error::Error for NetworkError {}

#[derive(Debug, Clone)]
pub struct NetworkResponse {
    pub final_url: Url,
    pub status: u16,
    pub mime: String,
    pub bytes: Vec<u8>,
}

#[derive(Debug, Default)]
pub struct NetworkPolicyBroker;

impl NetworkPolicyBroker {
    pub fn validate(&self, url: &Url, _purpose: RequestPurpose) -> Result<(), NetworkError> {
        match url.scheme() {
            "http" | "https" => {}
            other => {
                return Err(NetworkError::Policy(format!(
                    "scheme '{other}' is not allowed in Citra 0.1"
                )))
            }
        }

        if url.username() != "" || url.password().is_some() {
            return Err(NetworkError::Policy(
                "credentials embedded in URLs are blocked".into(),
            ));
        }

        if url.host_str().is_none() {
            return Err(NetworkError::Policy("URL has no host".into()));
        }

        Ok(())
    }
}

#[derive(Clone)]
pub struct NetworkBroker {
    client: Client,
    policy: std::sync::Arc<NetworkPolicyBroker>,
}

impl fmt::Debug for NetworkBroker {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("NetworkBroker").finish_non_exhaustive()
    }
}

impl NetworkBroker {
    pub fn new() -> Result<Self, NetworkError> {
        let client = Client::builder()
            .redirect(Policy::none())
            .connect_timeout(Duration::from_secs(10))
            .timeout(Duration::from_secs(25))
            .pool_idle_timeout(Duration::from_secs(30))
            .build()
            .map_err(|e| NetworkError::Transport(e.to_string()))?;

        Ok(Self {
            client,
            policy: std::sync::Arc::new(NetworkPolicyBroker),
        })
    }

    pub fn fetch(&self, start: Url, purpose: RequestPurpose) -> Result<NetworkResponse, NetworkError> {
        let max_bytes = match purpose {
            RequestPurpose::Navigation => MAX_DOCUMENT_BYTES,
            RequestPurpose::Image => MAX_IMAGE_BYTES,
        };

        let mut current = start;
        for _ in 0..=MAX_REDIRECTS {
            self.policy.validate(&current, purpose)?;
            let mut request = self
                .client
                .get(current.clone())
                .header(USER_AGENT, "Citra/0.1 Native")
                .header(ACCEPT_LANGUAGE, "en-US,en;q=0.5");

            request = match purpose {
                RequestPurpose::Navigation => request.header(
                    ACCEPT,
                    "text/html,application/xhtml+xml,text/plain;q=0.8,*/*;q=0.2",
                ),
                RequestPurpose::Image => request.header(
                    ACCEPT,
                    "image/avif,image/webp,image/png,image/jpeg,image/gif;q=0.9,*/*;q=0.1",
                ),
            };

            let response = request
                .send()
                .map_err(|e| NetworkError::Transport(e.to_string()))?;

            if response.status().is_redirection() {
                current = self.resolve_redirect(&current, &response)?;
                continue;
            }

            let status = response.status().as_u16();
            let final_url = response.url().clone();
            let mime = response
                .headers()
                .get(reqwest::header::CONTENT_TYPE)
                .and_then(|v| v.to_str().ok())
                .unwrap_or("application/octet-stream")
                .split(';')
                .next()
                .unwrap_or("application/octet-stream")
                .trim()
                .to_ascii_lowercase();

            let bytes = read_capped(response, max_bytes)?;
            return Ok(NetworkResponse {
                final_url,
                status,
                mime,
                bytes,
            });
        }

        Err(NetworkError::Redirect(format!(
            "more than {MAX_REDIRECTS} redirects"
        )))
    }

    fn resolve_redirect(&self, current: &Url, response: &Response) -> Result<Url, NetworkError> {
        let location = response
            .headers()
            .get(LOCATION)
            .ok_or_else(|| NetworkError::Redirect("redirect has no Location header".into()))?
            .to_str()
            .map_err(|_| NetworkError::Redirect("Location is not valid text".into()))?;
        current
            .join(location)
            .map_err(|e| NetworkError::Redirect(e.to_string()))
    }
}

fn read_capped(response: Response, max_bytes: usize) -> Result<Vec<u8>, NetworkError> {
    if let Some(length) = response.content_length() {
        if length > max_bytes as u64 {
            return Err(NetworkError::TooLarge { limit: max_bytes });
        }
    }

    let mut bytes = Vec::with_capacity(response.content_length().unwrap_or(0).min(max_bytes as u64) as usize);
    let mut limited = response.take((max_bytes + 1) as u64);
    limited
        .read_to_end(&mut bytes)
        .map_err(|e| NetworkError::Transport(e.to_string()))?;
    if bytes.len() > max_bytes {
        return Err(NetworkError::TooLarge { limit: max_bytes });
    }
    Ok(bytes)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn blocks_file_scheme() {
        let policy = NetworkPolicyBroker;
        let url = Url::parse("file:///etc/passwd").unwrap();
        assert!(policy.validate(&url, RequestPurpose::Navigation).is_err());
    }

    #[test]
    fn allows_https() {
        let policy = NetworkPolicyBroker;
        let url = Url::parse("https://example.com/").unwrap();
        assert!(policy.validate(&url, RequestPurpose::Navigation).is_ok());
    }
}
