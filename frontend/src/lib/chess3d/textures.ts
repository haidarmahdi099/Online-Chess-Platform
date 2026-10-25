import * as THREE from "three";

function grain(
  g: CanvasRenderingContext2D,
  x: number,
  y: number,
  w: number,
  h: number,
  base: string,
  ink: string,
  lines: number,
  scale: number,
) {
  g.fillStyle = base;
  g.fillRect(x, y, w, h);
  g.save();
  g.beginPath();
  g.rect(x, y, w, h);
  g.clip();
  const r = (a: number, b: number) => a + Math.random() * (b - a);
  for (let i = 0; i < lines; i++) {
    const yy = y + r(0, h);
    g.strokeStyle = `${ink}${r(0.03, 0.16)})`;
    g.lineWidth = r(1, 5) * scale;
    g.beginPath();
    g.moveTo(x, yy);
    g.bezierCurveTo(
      x + w * 0.3,
      yy + r(-14, 14) * scale,
      x + w * 0.7,
      yy + r(-14, 14) * scale,
      x + w,
      yy + r(-8, 8) * scale,
    );
    g.stroke();
  }
  g.restore();
}

let boardCache: THREE.CanvasTexture | null = null;
/** Procedural wood board. size = 4096 for 4K, use 2048 on weak/mobile GPUs. */
export function getBoardTexture(size: number, anisotropy: number) {
  if (boardCache) return boardCache;
  const c = document.createElement("canvas");
  c.width = c.height = size;
  const g = c.getContext("2d")!;
  const sq = size / 8;
  const scale = size / 4096;
  for (let r = 0; r < 8; r++)
    for (let f = 0; f < 8; f++) {
      // canvas row 0 is rank 8 (a8 is light, a1 is dark)
      const light = (r + f) % 2 === 0;
      grain(
        g,
        f * sq,
        r * sq,
        sq,
        sq,
        light ? "#e3c595" : "#6b4426",
        light ? "rgba(120,80,40," : "rgba(20,10,4,",
        140,
        scale,
      );
    }
  boardCache = new THREE.CanvasTexture(c);
  boardCache.colorSpace = THREE.SRGBColorSpace;
  boardCache.anisotropy = anisotropy;
  return boardCache;
}

let frameCache: THREE.CanvasTexture | null = null;
export function getFrameTexture() {
  if (frameCache) return frameCache;
  const c = document.createElement("canvas");
  c.width = c.height = 1024;
  grain(c.getContext("2d")!, 0, 0, 1024, 1024, "#3b2412", "rgba(10,5,2,", 140, 0.25);
  frameCache = new THREE.CanvasTexture(c);
  frameCache.colorSpace = THREE.SRGBColorSpace;
  return frameCache;
}

export function makeLabelTexture(text: string) {
  const c = document.createElement("canvas");
  c.width = c.height = 128;
  const g = c.getContext("2d")!;
  g.fillStyle = "#e3c595";
  g.font = "600 84px Georgia, serif";
  g.textAlign = "center";
  g.textBaseline = "middle";
  g.fillText(text, 64, 70);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

export function makeGlowTexture(rgb: string) {
  const c = document.createElement("canvas");
  c.width = c.height = 128;
  const g = c.getContext("2d")!;
  const grad = g.createRadialGradient(64, 64, 0, 64, 64, 64);
  grad.addColorStop(0, `rgba(${rgb},1)`);
  grad.addColorStop(1, `rgba(${rgb},0)`);
  g.fillStyle = grad;
  g.fillRect(0, 0, 128, 128);
  return new THREE.CanvasTexture(c);
}
