// ImageHelper removed: BitmapToByteArray relied on System.Drawing.Bitmap (Windows-only)
// and had no remaining call sites. Re-introduce with a cross-platform image library
// (e.g. SkiaSharp or ImageSharp) when image processing is genuinely needed.
