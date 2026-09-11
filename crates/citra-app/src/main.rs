#![cfg_attr(target_os = "windows", windows_subsystem = "windows")]

use citra_capsules::Capsule;
use citra_core::{CitraCore, CoreEvent, DisplayImage};
use citra_renderer::{Inline, RenderDocument, RenderNode};
use eframe::egui::{self, Color32, FontId, RichText, Stroke, TextureHandle, TextureOptions, Vec2};
use std::collections::HashMap;
use std::sync::mpsc::{self, Receiver, Sender};
use std::sync::Arc;
use std::time::Duration;

fn main() -> eframe::Result {
    let native_options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default()
            .with_title("Citra 0.1 Native")
            .with_inner_size([1024.0, 720.0])
            .with_min_inner_size([720.0, 480.0]),
        renderer: eframe::Renderer::Glow,
        centered: true,
        ..Default::default()
    };

    eframe::run_native(
        "Citra",
        native_options,
        Box::new(|cc| Ok(Box::new(CitraApp::new(cc)))),
    )
}

struct Tab {
    id: u64,
    capsule: Capsule,
    location: String,
    title: String,
    document: RenderDocument,
    navigation_id: u64,
    history: Vec<String>,
    history_index: usize,
    loading: bool,
    status: String,
    textures: HashMap<String, TextureHandle>,
    blocked_images: HashMap<String, String>,
}

impl Tab {
    fn new(id: u64, capsule: Capsule, document: RenderDocument) -> Self {
        Self {
            id,
            capsule,
            location: document.final_url.clone(),
            title: document.title.clone(),
            document,
            navigation_id: 0,
            history: vec!["citra://start".into()],
            history_index: 0,
            loading: false,
            status: "Ready".into(),
            textures: HashMap::new(),
            blocked_images: HashMap::new(),
        }
    }

    fn can_back(&self) -> bool {
        self.history_index > 0
    }

    fn can_forward(&self) -> bool {
        self.history_index + 1 < self.history.len()
    }
}

struct CitraApp {
    core: Arc<CitraCore>,
    tx: Sender<CoreEvent>,
    rx: Receiver<CoreEvent>,
    tabs: Vec<Tab>,
    active_tab: usize,
    next_tab_id: u64,
    location_edit: String,
    pending_navigation: Option<(u64, String, bool)>,
}

impl CitraApp {
    fn new(cc: &eframe::CreationContext<'_>) -> Self {
        configure_retro_style(&cc.egui_ctx);
        let core = CitraCore::new().expect("CitraCore initialization failed");
        let (tx, rx) = mpsc::channel();
        let capsule = core.new_anonymous_capsule();
        let start = core.start_document();
        Self {
            core,
            tx,
            rx,
            tabs: vec![Tab::new(1, capsule, start)],
            active_tab: 0,
            next_tab_id: 2,
            location_edit: "citra://start".into(),
            pending_navigation: None,
        }
    }

    fn active_tab(&self) -> &Tab {
        &self.tabs[self.active_tab]
    }

    fn active_tab_mut(&mut self) -> &mut Tab {
        &mut self.tabs[self.active_tab]
    }

    fn new_tab(&mut self) {
        let id = self.next_tab_id;
        self.next_tab_id += 1;
        let capsule = self.core.new_anonymous_capsule();
        let start = self.core.start_document();
        self.tabs.push(Tab::new(id, capsule, start));
        self.active_tab = self.tabs.len() - 1;
        self.location_edit = "citra://start".into();
    }

    fn close_active_tab(&mut self) {
        if self.tabs.len() == 1 {
            return;
        }
        self.tabs.remove(self.active_tab);
        self.active_tab = self.active_tab.min(self.tabs.len() - 1);
        self.location_edit = self.active_tab().location.clone();
    }

    fn queue_navigation(&mut self, input: impl Into<String>, push_history: bool) {
        let tab_id = self.active_tab().id;
        self.pending_navigation = Some((tab_id, input.into(), push_history));
    }

    fn start_queued_navigation(&mut self) {
        let Some((tab_id, input, push_history)) = self.pending_navigation.take() else {
            return;
        };
        let Some(index) = self.tabs.iter().position(|t| t.id == tab_id) else {
            return;
        };

        if input == "citra://start" {
            let tab = &mut self.tabs[index];
            tab.document = self.core.start_document();
            tab.location = "citra://start".into();
            tab.title = "Citra 0.1 Native".into();
            tab.status = "Ready".into();
            tab.loading = false;
            tab.textures.clear();
            tab.blocked_images.clear();
            if push_history {
                push_history_entry(tab, "citra://start".into());
            }
            if index == self.active_tab {
                self.location_edit = tab.location.clone();
            }
            return;
        }

        let navigation_id = self.core.next_navigation_id();
        let tab = &mut self.tabs[index];
        tab.navigation_id = navigation_id;
        tab.loading = true;
        tab.status = "Contacting host…".into();
        tab.textures.clear();
        tab.blocked_images.clear();
        if push_history {
            push_history_entry(tab, input.clone());
        }
        let capsule = tab.capsule.clone();
        self.core.spawn_navigation(
            tab_id,
            navigation_id,
            capsule,
            input,
            self.tx.clone(),
        );
    }

    fn go_back(&mut self) {
        let tab = self.active_tab_mut();
        if !tab.can_back() {
            return;
        }
        tab.history_index -= 1;
        let target = tab.history[tab.history_index].clone();
        self.queue_navigation(target, false);
    }

    fn go_forward(&mut self) {
        let tab = self.active_tab_mut();
        if !tab.can_forward() {
            return;
        }
        tab.history_index += 1;
        let target = tab.history[tab.history_index].clone();
        self.queue_navigation(target, false);
    }

    fn process_core_events(&mut self, ctx: &egui::Context) {
        while let Ok(event) = self.rx.try_recv() {
            match event {
                CoreEvent::NavigationStarted {
                    tab_id,
                    navigation_id,
                    requested_url,
                } => {
                    if let Some(tab) = current_target(&mut self.tabs, tab_id, navigation_id) {
                        tab.location = requested_url;
                        tab.status = "Loading document…".into();
                        if tab_id == self.active_tab().id {
                            self.location_edit = tab.location.clone();
                        }
                    }
                }
                CoreEvent::DocumentReady {
                    tab_id,
                    navigation_id,
                    document,
                    status,
                } => {
                    if let Some(tab) = current_target(&mut self.tabs, tab_id, navigation_id) {
                        tab.location = document.final_url.clone();
                        tab.title = document.title.clone();
                        tab.document = document;
                        tab.status = format!("{status} · waiting for visual gates");
                        tab.loading = false;
                        if tab_id == self.tabs[self.active_tab].id {
                            self.location_edit = tab.location.clone();
                        }
                    }
                }
                CoreEvent::ImageReady {
                    tab_id,
                    navigation_id,
                    resource_id,
                    image,
                    machine_reason,
                } => {
                    if let Some(tab) = current_target(&mut self.tabs, tab_id, navigation_id) {
                        let texture = load_texture(ctx, &resource_id, image);
                        tab.textures.insert(resource_id, texture);
                        tab.status = format!("Visual allowed · {machine_reason}");
                    }
                }
                CoreEvent::ImageBlocked {
                    tab_id,
                    navigation_id,
                    resource_id,
                    reason,
                } => {
                    if let Some(tab) = current_target(&mut self.tabs, tab_id, navigation_id) {
                        tab.blocked_images.insert(resource_id, reason.clone());
                        tab.status = format!("Visual quarantined · {reason}");
                    }
                }
                CoreEvent::NavigationFailed {
                    tab_id,
                    navigation_id,
                    message,
                } => {
                    if let Some(tab) = current_target(&mut self.tabs, tab_id, navigation_id) {
                        tab.loading = false;
                        tab.status = format!("Error: {message}");
                        tab.document = error_document(&tab.location, &message);
                    }
                }
            }
        }
    }

    fn render_document(&mut self, ui: &mut egui::Ui) {
        let active = self.active_tab;
        let nodes = self.tabs[active].document.nodes.clone();
        let textures = self.tabs[active].textures.clone();
        let blocked = self.tabs[active].blocked_images.clone();
        let mut navigate_to: Option<String> = None;

        egui::ScrollArea::vertical()
            .auto_shrink([false, false])
            .show(ui, |ui| {
                ui.set_max_width(900.0);
                ui.add_space(8.0);
                for node in nodes {
                    match node {
                        RenderNode::Heading { level, text } => {
                            let size = match level {
                                1 => 28.0,
                                2 => 24.0,
                                3 => 21.0,
                                4 => 18.0,
                                _ => 16.0,
                            };
                            ui.label(RichText::new(text).font(FontId::proportional(size)).strong());
                            ui.add_space(4.0);
                        }
                        RenderNode::Paragraph { inlines } => {
                            render_inlines(ui, &inlines, &mut navigate_to);
                            ui.add_space(8.0);
                        }
                        RenderNode::ListItem { inlines } => {
                            ui.horizontal_wrapped(|ui| {
                                ui.label("•");
                                render_inlines(ui, &inlines, &mut navigate_to);
                            });
                            ui.add_space(3.0);
                        }
                        RenderNode::Preformatted(text) => {
                            egui::Frame::new()
                                .fill(Color32::from_gray(245))
                                .stroke(Stroke::new(1.0, Color32::from_gray(150)))
                                .inner_margin(6.0)
                                .show(ui, |ui| {
                                    ui.label(RichText::new(text).monospace());
                                });
                            ui.add_space(8.0);
                        }
                        RenderNode::Rule => {
                            ui.separator();
                            ui.add_space(6.0);
                        }
                        RenderNode::Image {
                            resource_id,
                            alt,
                            url: _,
                        } => {
                            if let Some(texture) = textures.get(&resource_id) {
                                ui.add(
                                    egui::Image::from_texture(texture)
                                        .max_width(ui.available_width().min(800.0))
                                        .max_height(600.0)
                                        .alt_text(alt.clone()),
                                );
                            } else if let Some(reason) = blocked.get(&resource_id) {
                                visual_placeholder(ui, &alt, reason);
                            } else {
                                visual_placeholder(ui, &alt, "Visual quarantine: awaiting MiniMachine");
                            }
                            ui.add_space(8.0);
                        }
                    }
                }
                ui.add_space(24.0);
            });

        if let Some(url) = navigate_to {
            self.queue_navigation(url, true);
        }
    }
}

impl eframe::App for CitraApp {
    fn logic(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.process_core_events(ctx);
        self.start_queued_navigation();
        if self.tabs.iter().any(|t| t.loading) {
            ctx.request_repaint_after(Duration::from_millis(80));
        }
    }

    fn ui(&mut self, ui: &mut egui::Ui, _frame: &mut eframe::Frame) {
        let ctx = ui.ctx().clone();
        let ctrl = ctx.input(|i| i.modifiers.ctrl);
        if ctrl && ctx.input(|i| i.key_pressed(egui::Key::T)) {
            self.new_tab();
        }
        if ctrl && ctx.input(|i| i.key_pressed(egui::Key::W)) {
            self.close_active_tab();
        }
        if ctrl && ctx.input(|i| i.key_pressed(egui::Key::L)) {
            ctx.memory_mut(|m| m.request_focus(egui::Id::new("citra-location")));
        }

        ui.vertical(|ui| {
            retro_menu_bar(ui);
            ui.separator();

            ui.horizontal(|ui| {
                if ui.add_enabled(self.active_tab().can_back(), egui::Button::new("◀ Back")).clicked() {
                    self.go_back();
                }
                if ui.add_enabled(self.active_tab().can_forward(), egui::Button::new("Forward ▶")).clicked() {
                    self.go_forward();
                }
                if ui.button("Home").clicked() {
                    self.queue_navigation("citra://start", true);
                }
                if ui.button("Reload").clicked() {
                    let target = self.active_tab().location.clone();
                    self.queue_navigation(target, false);
                }
                ui.separator();
                ui.label("Location:");
                let response = ui.add_sized(
                    [ui.available_width().max(160.0), 24.0],
                    egui::TextEdit::singleline(&mut self.location_edit)
                        .id(egui::Id::new("citra-location"))
                        .font(egui::TextStyle::Monospace),
                );
                if response.lost_focus() && ui.input(|i| i.key_pressed(egui::Key::Enter)) {
                    let target = self.location_edit.clone();
                    self.queue_navigation(target, true);
                }
            });

            ui.horizontal(|ui| {
                for index in 0..self.tabs.len() {
                    let selected = index == self.active_tab;
                    let title = compact_title(&self.tabs[index].title);
                    if ui.selectable_label(selected, title).clicked() {
                        self.active_tab = index;
                        self.location_edit = self.active_tab().location.clone();
                    }
                }
                if ui.button("+").on_hover_text("New anonymous tab (Ctrl+T)").clicked() {
                    self.new_tab();
                }
            });
            ui.separator();

            let body_height = (ui.available_height() - 32.0).max(100.0);
            ui.allocate_ui_with_layout(
                Vec2::new(ui.available_width(), body_height),
                egui::Layout::top_down(egui::Align::Min),
                |ui| self.render_document(ui),
            );

            ui.separator();
            ui.horizontal(|ui| {
                let health = self.core.machine_health();
                let machine_color = if health.label() == "ONLINE" {
                    Color32::DARK_GREEN
                } else {
                    Color32::DARK_RED
                };
                ui.colored_label(machine_color, format!("MiniMachine: {}", health.label()));
                ui.separator();
                ui.label(format!("Capsule: {} (RAM-only)", self.active_tab().capsule.name));
                ui.separator();
                ui.label(&self.active_tab().status);
                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let mut dev = self.core.dev_allow_visuals();
                    if ui.checkbox(&mut dev, "DEV ALLOW VISUALS").changed() {
                        self.core.set_dev_allow_visuals(dev);
                    }
                });
            });
        });
    }
}

fn current_target(tabs: &mut [Tab], tab_id: u64, navigation_id: u64) -> Option<&mut Tab> {
    tabs.iter_mut()
        .find(|tab| tab.id == tab_id && tab.navigation_id == navigation_id)
}

fn push_history_entry(tab: &mut Tab, location: String) {
    if tab.history_index + 1 < tab.history.len() {
        tab.history.truncate(tab.history_index + 1);
    }
    if tab.history.last().map(|s| s.as_str()) != Some(location.as_str()) {
        tab.history.push(location);
        tab.history_index = tab.history.len() - 1;
    }
}

fn load_texture(ctx: &egui::Context, resource_id: &str, image: DisplayImage) -> TextureHandle {
    let color = egui::ColorImage::from_rgba_unmultiplied(
        [image.width as usize, image.height as usize],
        &image.rgba,
    );
    ctx.load_texture(
        format!("citra-{resource_id}"),
        color,
        TextureOptions::LINEAR,
    )
}

fn render_inlines(ui: &mut egui::Ui, inlines: &[Inline], navigate_to: &mut Option<String>) {
    ui.horizontal_wrapped(|ui| {
        for inline in inlines {
            match inline {
                Inline::Text(text) => {
                    ui.label(text);
                }
                Inline::Link { text, url } => {
                    if ui.link(text).clicked() {
                        *navigate_to = Some(url.clone());
                    }
                }
                Inline::LineBreak => {
                    ui.end_row();
                }
            }
        }
    });
}

fn visual_placeholder(ui: &mut egui::Ui, alt: &str, reason: &str) {
    egui::Frame::new()
        .fill(Color32::from_gray(220))
        .stroke(Stroke::new(1.0, Color32::from_gray(120)))
        .inner_margin(8.0)
        .show(ui, |ui| {
            ui.set_min_size(Vec2::new(260.0, 70.0));
            ui.label(RichText::new("[ visual not presented ]").monospace().strong());
            if !alt.trim().is_empty() {
                ui.label(format!("Alt: {alt}"));
            }
            ui.label(RichText::new(reason).small().weak());
        });
}

fn retro_menu_bar(ui: &mut egui::Ui) {
    ui.horizontal(|ui| {
        for item in ["File", "Edit", "View", "Go", "Help"] {
            ui.add(egui::Button::new(item).frame(false));
        }
        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
            ui.label(RichText::new("CITRA 0.1 NATIVE").monospace().strong());
        });
    });
}

fn configure_retro_style(ctx: &egui::Context) {
    let mut visuals = egui::Visuals::light();
    visuals.panel_fill = Color32::from_rgb(236, 233, 216);
    visuals.window_fill = Color32::from_rgb(236, 233, 216);
    visuals.extreme_bg_color = Color32::WHITE;
    visuals.hyperlink_color = Color32::from_rgb(0, 0, 200);
    visuals.widgets.noninteractive.bg_fill = Color32::from_rgb(236, 233, 216);
    visuals.widgets.inactive.bg_fill = Color32::from_rgb(236, 233, 216);
    visuals.widgets.hovered.bg_fill = Color32::from_rgb(250, 250, 245);
    visuals.widgets.active.bg_fill = Color32::WHITE;
    ctx.set_visuals(visuals);

    let mut style = (*ctx.style()).clone();
    style.spacing.item_spacing = Vec2::new(6.0, 4.0);
    style.spacing.button_padding = Vec2::new(8.0, 3.0);
    style.visuals.widgets.noninteractive.corner_radius = egui::CornerRadius::ZERO;
    style.visuals.widgets.inactive.corner_radius = egui::CornerRadius::ZERO;
    style.visuals.widgets.hovered.corner_radius = egui::CornerRadius::ZERO;
    style.visuals.widgets.active.corner_radius = egui::CornerRadius::ZERO;
    ctx.set_style(style);
}

fn compact_title(title: &str) -> String {
    let mut chars = title.chars();
    let compact: String = chars.by_ref().take(24).collect();
    if chars.next().is_some() {
        format!("{compact}…")
    } else {
        compact
    }
}

fn error_document(location: &str, message: &str) -> RenderDocument {
    RenderDocument {
        title: "Citra - Error".into(),
        final_url: location.into(),
        nodes: vec![
            RenderNode::Heading {
                level: 2,
                text: "Unable to load page".into(),
            },
            RenderNode::Preformatted(message.into()),
        ],
        images: Vec::new(),
    }
}
