use image::imageops::FilterType;
use image::{GenericImageView, ImageReader, Limits};
use std::io::{Cursor, Read, Write};

const MAX_INPUT_BYTES: usize = 8 * 1024 * 1024;
const MAX_DIMENSION: u32 = 4096;
const MAX_ALLOC: u64 = 96 * 1024 * 1024;
const DISPLAY_MAX_W: u32 = 1600;
const DISPLAY_MAX_H: u32 = 1200;
const PREVIEW_MAX: u32 = 128;

fn main() {
    let result = std::panic::catch_unwind(run);
    match result {
        Ok(Ok(())) => {}
        Ok(Err(error)) => {
            eprintln!("citra-image-decoder: {error}");
            std::process::exit(2);
        }
        Err(_) => {
            eprintln!("citra-image-decoder: decoder panic contained in worker process");
            std::process::exit(3);
        }
    }
}

fn run() -> Result<(), String> {
    let mut input = Vec::new();
    std::io::stdin()
        .take((MAX_INPUT_BYTES + 1) as u64)
        .read_to_end(&mut input)
        .map_err(|e| e.to_string())?;
    if input.len() > MAX_INPUT_BYTES {
        return Err("compressed image exceeds input limit".into());
    }

    let mut reader = ImageReader::new(Cursor::new(&input));
    reader = reader.with_guessed_format().map_err(|e| e.to_string())?;
    let mut limits = Limits::default();
    limits.max_image_width = Some(MAX_DIMENSION);
    limits.max_image_height = Some(MAX_DIMENSION);
    limits.max_alloc = Some(MAX_ALLOC);
    reader.limits(limits);

    let image = reader.decode().map_err(|e| e.to_string())?;
    let (width, height) = image.dimensions();
    if width == 0 || height == 0 || width > MAX_DIMENSION || height > MAX_DIMENSION {
        return Err(format!("invalid dimensions: {width}x{height}"));
    }

    let display = if width > DISPLAY_MAX_W || height > DISPLAY_MAX_H {
        image.resize(DISPLAY_MAX_W, DISPLAY_MAX_H, FilterType::Lanczos3)
    } else {
        image.clone()
    };
    let preview = image.thumbnail(PREVIEW_MAX, PREVIEW_MAX);

    let display = display.into_rgba8();
    let preview = preview.into_rgba8();
    let (display_w, display_h) = display.dimensions();
    let (preview_w, preview_h) = preview.dimensions();
    let display = display.into_raw();
    let preview = preview.into_raw();

    let mut stdout = std::io::BufWriter::new(std::io::stdout().lock());
    stdout.write_all(b"CIMG")?;
    write_u32(&mut stdout, 1)?;
    write_u32(&mut stdout, display_w)?;
    write_u32(&mut stdout, display_h)?;
    write_u32(&mut stdout, preview_w)?;
    write_u32(&mut stdout, preview_h)?;
    write_u64(&mut stdout, display.len() as u64)?;
    write_u64(&mut stdout, preview.len() as u64)?;
    stdout.write_all(&display)?;
    stdout.write_all(&preview)?;
    stdout.flush()?;
    Ok(())
}

fn write_u32(w: &mut impl Write, value: u32) -> Result<(), String> {
    w.write_all(&value.to_le_bytes()).map_err(|e| e.to_string())
}

fn write_u64(w: &mut impl Write, value: u64) -> Result<(), String> {
    w.write_all(&value.to_le_bytes()).map_err(|e| e.to_string())
}
