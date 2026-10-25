import * as THREE from "three";
import type { PieceColor } from "@/lib/types";

const P = (a: number[][]) => a.map(([x, y]) => new THREE.Vector2(x!, y!));
const PROFILES: Record<string, THREE.Vector2[]> = {
  p: P([
    [0, 0],
    [0.34, 0],
    [0.36, 0.06],
    [0.28, 0.12],
    [0.16, 0.3],
    [0.2, 0.36],
    [0.13, 0.42],
    [0.19, 0.5],
    [0.2, 0.6],
    [0.14, 0.7],
    [0, 0.74],
  ]),
  r: P([
    [0, 0],
    [0.38, 0],
    [0.4, 0.08],
    [0.3, 0.16],
    [0.26, 0.6],
    [0.34, 0.68],
    [0.34, 0.9],
    [0, 0.9],
  ]),
  n: P([
    [0, 0],
    [0.38, 0],
    [0.4, 0.08],
    [0.3, 0.16],
    [0.26, 0.38],
    [0.3, 0.44],
    [0, 0.44],
  ]),
  b: P([
    [0, 0],
    [0.38, 0],
    [0.4, 0.08],
    [0.28, 0.16],
    [0.15, 0.4],
    [0.22, 0.5],
    [0.13, 0.6],
    [0.25, 0.82],
    [0.2, 1.02],
    [0.05, 1.12],
    [0, 1.12],
  ]),
  q: P([
    [0, 0],
    [0.4, 0],
    [0.42, 0.08],
    [0.3, 0.16],
    [0.17, 0.5],
    [0.26, 0.62],
    [0.15, 0.74],
    [0.32, 1.0],
    [0.28, 1.18],
    [0.1, 1.22],
    [0, 1.22],
  ]),
  k: P([
    [0, 0],
    [0.42, 0],
    [0.44, 0.08],
    [0.32, 0.16],
    [0.18, 0.55],
    [0.28, 0.68],
    [0.16, 0.8],
    [0.32, 1.08],
    [0.2, 1.2],
    [0, 1.22],
  ]),
};

const geoCache = new Map<string, THREE.BufferGeometry>();
const geo = (key: string, make: () => THREE.BufferGeometry) => {
  let g = geoCache.get(key);
  if (!g) geoCache.set(key, (g = make()));
  return g;
};

function knightHead() {
  const s = new THREE.Shape();
  s.moveTo(-0.22, 0.4);
  s.lineTo(-0.27, 0.7);
  s.quadraticCurveTo(-0.3, 1.0, -0.1, 1.1);
  s.lineTo(-0.06, 1.24);
  s.lineTo(0.03, 1.13);
  s.quadraticCurveTo(0.14, 1.17, 0.32, 0.92);
  s.lineTo(0.35, 0.8);
  s.quadraticCurveTo(0.3, 0.73, 0.2, 0.76);
  s.lineTo(0.03, 0.68);
  s.quadraticCurveTo(0.14, 0.5, 0.24, 0.4);
  s.closePath();
  const g = new THREE.ExtrudeGeometry(s, {
    depth: 0.2,
    bevelEnabled: true,
    bevelSize: 0.04,
    bevelThickness: 0.04,
    bevelSegments: 3,
    curveSegments: 12,
  });
  g.translate(0, 0, -0.1);
  return g;
}

let mats: Record<PieceColor, THREE.MeshPhysicalMaterial> | null = null;
const getMats = () =>
  (mats ??= {
    w: new THREE.MeshPhysicalMaterial({
      color: 0xf0e8d6,
      roughness: 0.32,
      metalness: 0.04,
      clearcoat: 0.4,
      clearcoatRoughness: 0.35,
    }),
    b: new THREE.MeshPhysicalMaterial({
      color: 0x2a211b,
      roughness: 0.3,
      metalness: 0.08,
      clearcoat: 0.5,
      clearcoatRoughness: 0.3,
    }),
  });

export function buildPiece(type: string, color: PieceColor) {
  const m = getMats()[color];
  const g = new THREE.Group();
  const add = (geometry: THREE.BufferGeometry, x = 0, y = 0, z = 0) => {
    const mesh = new THREE.Mesh(geometry, m);
    mesh.position.set(x, y, z);
    mesh.castShadow = mesh.receiveShadow = true;
    g.add(mesh);
    return mesh;
  };
  add(geo(`lathe-${type}`, () => new THREE.LatheGeometry(PROFILES[type]!, 40)));
  if (type === "r") {
    const merlon = geo("merlon", () => new THREE.BoxGeometry(0.16, 0.14, 0.16));
    for (let i = 0; i < 4; i++) {
      const a = (i * Math.PI) / 2 + Math.PI / 4;
      add(merlon, Math.cos(a) * 0.27, 0.97, Math.sin(a) * 0.27).rotation.y = -a;
    }
  } else if (type === "n") {
    add(geo("knight", knightHead));
    g.rotation.y = color === "w" ? Math.PI / 2 : -Math.PI / 2; // face the opponent
  } else if (type === "b")
    add(
      geo("bishop-top", () => new THREE.SphereGeometry(0.07, 16, 12)),
      0,
      1.17,
      0,
    );
  else if (type === "q")
    add(
      geo("queen-top", () => new THREE.SphereGeometry(0.1, 20, 16)),
      0,
      1.3,
      0,
    );
  else if (type === "k") {
    add(
      geo("cross-v", () => new THREE.BoxGeometry(0.08, 0.3, 0.08)),
      0,
      1.38,
      0,
    );
    add(
      geo("cross-h", () => new THREE.BoxGeometry(0.24, 0.08, 0.08)),
      0,
      1.4,
      0,
    );
  }
  // wrapper so the knight's facing rotation doesn't fight position/scale
  const root = new THREE.Group();
  root.add(g);
  return root;
}
