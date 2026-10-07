// The page is drawn at a fixed baseline zoom (styles.css: --ui-zoom on :root, 150% with a mouse, 100% on touch screens).
// Fixed-position popovers place themselves in page px, so they divide the screen coordinates of their anchor by it.

/** The zoom the page is drawn at (CSS zoom on <html>; browser zoom is separate and needs no correction). */
export function currentZoom() {
  return parseFloat(getComputedStyle(document.documentElement).zoom) || 1;
}
