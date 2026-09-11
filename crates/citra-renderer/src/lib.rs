use html5ever::tendril::StrTendril;
use html5ever::tokenizer::{
    BufferQueue, EndTag, StartTag, Tag, Token, TokenSink, TokenSinkResult, Tokenizer,
    TokenizerOpts,
};
use std::cell::RefCell;
use url::Url;

#[derive(Debug, Clone)]
pub struct RenderDocument {
    pub title: String,
    pub final_url: String,
    pub nodes: Vec<RenderNode>,
    pub images: Vec<ImageRequest>,
}

#[derive(Debug, Clone)]
pub enum RenderNode {
    Heading { level: u8, text: String },
    Paragraph { inlines: Vec<Inline> },
    Preformatted(String),
    ListItem { inlines: Vec<Inline> },
    Image {
        resource_id: String,
        alt: String,
        url: String,
    },
    Rule,
}

#[derive(Debug, Clone)]
pub enum Inline {
    Text(String),
    Link { text: String, url: String },
    LineBreak,
}

#[derive(Debug, Clone)]
pub struct ImageRequest {
    pub resource_id: String,
    pub url: String,
    pub alt: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
enum BlockKind {
    Paragraph,
    Heading(u8),
    ListItem,
    Pre,
}

#[derive(Debug)]
struct ParserState {
    base_url: Url,
    title: String,
    in_title: bool,
    hidden_depth: usize,
    current_block: Option<BlockKind>,
    current_inlines: Vec<Inline>,
    current_pre: String,
    active_link: Option<String>,
    active_link_text: String,
    nodes: Vec<RenderNode>,
    images: Vec<ImageRequest>,
    next_image_id: u64,
}

impl ParserState {
    fn new(base_url: Url) -> Self {
        Self {
            base_url,
            title: String::new(),
            in_title: false,
            hidden_depth: 0,
            current_block: None,
            current_inlines: Vec::new(),
            current_pre: String::new(),
            active_link: None,
            active_link_text: String::new(),
            nodes: Vec::new(),
            images: Vec::new(),
            next_image_id: 1,
        }
    }

    fn begin_block(&mut self, kind: BlockKind) {
        self.flush_block();
        self.current_block = Some(kind);
    }

    fn append_text(&mut self, text: &str) {
        if self.hidden_depth > 0 {
            return;
        }
        if self.in_title {
            self.title.push_str(text);
            return;
        }

        if self.current_block == Some(BlockKind::Pre) {
            self.current_pre.push_str(text);
            return;
        }

        if self.current_block.is_none() {
            self.current_block = Some(BlockKind::Paragraph);
        }

        if self.active_link.is_some() {
            self.active_link_text.push_str(text);
        } else {
            push_text_inline(&mut self.current_inlines, text);
        }
    }

    fn flush_link(&mut self) {
        if let Some(url) = self.active_link.take() {
            let text = normalize_inline_text(&self.active_link_text);
            self.active_link_text.clear();
            if !text.is_empty() {
                self.current_inlines.push(Inline::Link { text, url });
            }
        }
    }

    fn flush_block(&mut self) {
        self.flush_link();
        let Some(kind) = self.current_block.take() else {
            self.current_inlines.clear();
            self.current_pre.clear();
            return;
        };

        match kind {
            BlockKind::Pre => {
                let text = self.current_pre.trim_matches('\n').to_string();
                if !text.trim().is_empty() {
                    self.nodes.push(RenderNode::Preformatted(text));
                }
            }
            BlockKind::Paragraph => {
                compact_inlines(&mut self.current_inlines);
                if !self.current_inlines.is_empty() {
                    self.nodes.push(RenderNode::Paragraph {
                        inlines: std::mem::take(&mut self.current_inlines),
                    });
                }
            }
            BlockKind::Heading(level) => {
                let text = inline_plain_text(&self.current_inlines);
                self.current_inlines.clear();
                if !text.is_empty() {
                    self.nodes.push(RenderNode::Heading { level, text });
                }
            }
            BlockKind::ListItem => {
                compact_inlines(&mut self.current_inlines);
                if !self.current_inlines.is_empty() {
                    self.nodes.push(RenderNode::ListItem {
                        inlines: std::mem::take(&mut self.current_inlines),
                    });
                }
            }
        }
        self.current_pre.clear();
        self.current_inlines.clear();
    }

    fn add_image(&mut self, src: &str, alt: &str) {
        self.flush_link();
        self.flush_block();
        let Ok(url) = self.base_url.join(src) else {
            return;
        };
        if !matches!(url.scheme(), "http" | "https") {
            return;
        }
        let resource_id = format!("img-{}", self.next_image_id);
        self.next_image_id += 1;
        let url = url.to_string();
        let alt = alt.trim().to_string();
        self.images.push(ImageRequest {
            resource_id: resource_id.clone(),
            url: url.clone(),
            alt: alt.clone(),
        });
        self.nodes.push(RenderNode::Image {
            resource_id,
            alt,
            url,
        });
    }

    fn set_base(&mut self, href: &str) {
        if let Ok(url) = self.base_url.join(href) {
            if matches!(url.scheme(), "http" | "https") {
                self.base_url = url;
            }
        }
    }
}

#[derive(Debug)]
struct FlatSink {
    state: RefCell<ParserState>,
}

impl FlatSink {
    fn new(base_url: Url) -> Self {
        Self {
            state: RefCell::new(ParserState::new(base_url)),
        }
    }
}

impl TokenSink for FlatSink {
    type Handle = ();

    fn process_token(&self, token: Token, _line_number: u64) -> TokenSinkResult<Self::Handle> {
        let mut state = self.state.borrow_mut();
        match token {
            Token::CharacterTokens(text) => state.append_text(&text),
            Token::TagToken(tag) => process_tag(&mut state, tag),
            Token::EOFToken => state.flush_block(),
            _ => {}
        }
        TokenSinkResult::Continue
    }
}

pub fn parse_document(html: &str, final_url: &Url) -> RenderDocument {
    let sink = FlatSink::new(final_url.clone());
    let tokenizer = Tokenizer::new(sink, TokenizerOpts::default());
    let input = BufferQueue::default();
    input.push_back(StrTendril::from_slice(html));
    let _ = tokenizer.feed(&input);
    tokenizer.end();

    let mut state = tokenizer.sink.state.into_inner();
    state.flush_block();

    let title = normalize_inline_text(&state.title);
    RenderDocument {
        title: if title.is_empty() { final_url.host_str().unwrap_or("Citra").to_string() } else { title },
        final_url: final_url.to_string(),
        nodes: state.nodes,
        images: state.images,
    }
}

pub fn start_document() -> RenderDocument {
    RenderDocument {
        title: "Citra 0.1 Native".into(),
        final_url: "citra://start".into(),
        nodes: vec![
            RenderNode::Heading {
                level: 1,
                text: "Citra 0.1 Native".into(),
            },
            RenderNode::Paragraph {
                inlines: vec![Inline::Text(
                    "A small native browser chassis. No Chromium. No JavaScript. No disk cache. Visual resources are gated by MiniMachine before presentation.".into(),
                )],
            },
            RenderNode::Rule,
            RenderNode::Paragraph {
                inlines: vec![
                    Inline::Text("Try: ".into()),
                    Inline::Link {
                        text: "example.com".into(),
                        url: "https://example.com/".into(),
                    },
                    Inline::Text("   ".into()),
                    Inline::Link {
                        text: "Rust".into(),
                        url: "https://www.rust-lang.org/".into(),
                    },
                ],
            },
        ],
        images: Vec::new(),
    }
}

fn process_tag(state: &mut ParserState, tag: Tag) {
    let name = tag.name.as_ref();
    match tag.kind {
        StartTag => match name {
            "script" | "style" | "noscript" | "template" => state.hidden_depth += 1,
            "title" => state.in_title = true,
            "base" => {
                if let Some(href) = attr(&tag, "href") {
                    state.set_base(&href);
                }
            }
            "h1" => state.begin_block(BlockKind::Heading(1)),
            "h2" => state.begin_block(BlockKind::Heading(2)),
            "h3" => state.begin_block(BlockKind::Heading(3)),
            "h4" => state.begin_block(BlockKind::Heading(4)),
            "h5" => state.begin_block(BlockKind::Heading(5)),
            "h6" => state.begin_block(BlockKind::Heading(6)),
            "p" | "div" | "section" | "article" | "header" | "footer" | "nav" | "main" | "blockquote" => {
                state.begin_block(BlockKind::Paragraph)
            }
            "li" => state.begin_block(BlockKind::ListItem),
            "pre" => state.begin_block(BlockKind::Pre),
            "br" => {
                if state.current_block == Some(BlockKind::Pre) {
                    state.current_pre.push('\n');
                } else {
                    state.current_inlines.push(Inline::LineBreak);
                }
            }
            "hr" => {
                state.flush_block();
                state.nodes.push(RenderNode::Rule);
            }
            "a" => {
                state.flush_link();
                if let Some(href) = attr(&tag, "href") {
                    if let Ok(url) = state.base_url.join(&href) {
                        if matches!(url.scheme(), "http" | "https") {
                            state.active_link = Some(url.to_string());
                        }
                    }
                }
            }
            "img" => {
                if let Some(src) = attr(&tag, "src") {
                    let alt = attr(&tag, "alt").unwrap_or_default();
                    state.add_image(&src, &alt);
                }
            }
            "td" | "th" => {
                if !state.current_inlines.is_empty() {
                    push_text_inline(&mut state.current_inlines, "  ");
                }
            }
            _ => {}
        },
        EndTag => match name {
            "script" | "style" | "noscript" | "template" => {
                state.hidden_depth = state.hidden_depth.saturating_sub(1)
            }
            "title" => state.in_title = false,
            "a" => state.flush_link(),
            "h1" | "h2" | "h3" | "h4" | "h5" | "h6" | "p" | "div" | "section"
            | "article" | "header" | "footer" | "nav" | "main" | "blockquote" | "li" | "pre" => {
                state.flush_block()
            }
            _ => {}
        },
    }
}

fn attr(tag: &Tag, wanted: &str) -> Option<String> {
    tag.attrs
        .iter()
        .find(|a| a.name.local.as_ref() == wanted)
        .map(|a| a.value.to_string())
}

fn push_text_inline(inlines: &mut Vec<Inline>, text: &str) {
    if text.is_empty() {
        return;
    }
    match inlines.last_mut() {
        Some(Inline::Text(existing)) => existing.push_str(text),
        _ => inlines.push(Inline::Text(text.to_string())),
    }
}

fn compact_inlines(inlines: &mut Vec<Inline>) {
    for inline in inlines.iter_mut() {
        match inline {
            Inline::Text(text) | Inline::Link { text, .. } => {
                *text = normalize_inline_text(text);
            }
            Inline::LineBreak => {}
        }
    }
    inlines.retain(|inline| match inline {
        Inline::Text(text) | Inline::Link { text, .. } => !text.is_empty(),
        Inline::LineBreak => true,
    });
}

fn inline_plain_text(inlines: &[Inline]) -> String {
    let mut out = String::new();
    for inline in inlines {
        match inline {
            Inline::Text(text) | Inline::Link { text, .. } => out.push_str(text),
            Inline::LineBreak => out.push(' '),
        }
    }
    normalize_inline_text(&out)
}

fn normalize_inline_text(text: &str) -> String {
    text.split_whitespace().collect::<Vec<_>>().join(" ")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn parses_links_and_ignores_script_text() {
        let url = Url::parse("https://example.com/a/").unwrap();
        let doc = parse_document(
            r#"<html><head><title>Hello</title><script>evil()</script></head><body><h1>Hi</h1><p>Go <a href="/next">next</a></p></body></html>"#,
            &url,
        );
        assert_eq!(doc.title, "Hello");
        let debug = format!("{doc:?}");
        assert!(debug.contains("https://example.com/next"));
        assert!(!debug.contains("evil"));
    }
}
