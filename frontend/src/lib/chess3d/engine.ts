import * as THREE from "three";
import { OrbitControls } from "three/examples/jsm/controls/OrbitControls.js";
import { RoomEnvironment } from "three/examples/jsm/environments/RoomEnvironment.js";
import type { BoardPiece } from "@/lib/game/board";
import type { PieceColor } from "@/lib/types";
import { buildPiece } from "./pieces";
import { getBoardTexture, getFrameTexture, makeGlowTexture, makeLabelTexture } from "./textures";

export interface EngineState {
  board: BoardPiece[];
  orientation: PieceColor;
  selected: string | null;
  legalTargets: string[];
  lastMove: { from: string; to: string } | null;
  checkSquare: string | null;
  perspective: boolean;
  interactive: boolean;
}

export interface Engine {
  update(s: EngineState): void;
  setOnSquareClick(fn: (square: string) => void): void;
  dispose(): void;
}

interface Entry {
  obj: THREE.Group;
  type: string;
  color: PieceColor;
  square: string;
  target: THREE.Vector3;
  scale: number;
  scaleGoal: number;
  leaving: boolean;
}

const FILES = "abcdefgh";
const toPos = (sq: string) =>
  new THREE.Vector3(FILES.indexOf(sq[0]!) - 3.5, 0, 3.5 - (Number(sq[1]) - 1));
const toSquare = (x: number, z: number) => {
  const f = Math.floor(x + 4);
  const r = Math.floor(4 - z);
  return f < 0 || f > 7 || r < 0 || r > 7 ? null : `${FILES[f]}${r + 1}`;
};
const wrapPi = (a: number) => Math.atan2(Math.sin(a), Math.cos(a));

export function createEngine(host: HTMLElement): Engine {
  const coarse = window.matchMedia("(pointer: coarse)").matches;
  const renderer = new THREE.WebGLRenderer({
    antialias: true,
    alpha: true,
    powerPreference: "high-performance",
  });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = 1.05;
  const canvas = renderer.domElement;
  canvas.style.cssText =
    "position:absolute;inset:0;width:100%;height:100%;display:block;touch-action:none;outline:none";
  host.appendChild(canvas);

  const scene = new THREE.Scene();
  const pmrem = new THREE.PMREMGenerator(renderer);
  const envTex = pmrem.fromScene(new RoomEnvironment(), 0.04).texture;
  scene.environment = envTex;
  scene.environmentIntensity = 0.55;

  const camera = new THREE.PerspectiveCamera(32, 1, 0.1, 100);
  const controls = new OrbitControls(camera, canvas);
  controls.enableDamping = true;
  controls.dampingFactor = 0.08;
  controls.enablePan = false;
  controls.minDistance = 9;
  controls.maxDistance = 24;
  controls.minPolarAngle = 0.03;
  controls.maxPolarAngle = 1.35;

  scene.add(new THREE.HemisphereLight(0xfff3df, 0x3a2f25, 0.45));
  const sun = new THREE.DirectionalLight(0xffefd5, 1.6);
  sun.position.set(6, 12, 5);
  sun.castShadow = true;
  sun.shadow.mapSize.set(coarse ? 1024 : 2048, coarse ? 1024 : 2048);
  Object.assign(sun.shadow.camera, { left: -7, right: 7, top: 7, bottom: -7, near: 1, far: 30 });
  sun.shadow.camera.updateProjectionMatrix(); // required after resizing the frustum
  sun.shadow.bias = -0.0004;
  scene.add(sun);

  // ---- board ----
  const maxTex = renderer.capabilities.maxTextureSize;
  const texSize = !coarse && maxTex >= 4096 ? 4096 : 2048;
  const frameTex = getFrameTexture();
  const frame = new THREE.Mesh(
    new THREE.BoxGeometry(9.4, 0.5, 9.4),
    new THREE.MeshStandardMaterial({ map: frameTex, roughness: 0.45, metalness: 0.02 }),
  );
  frame.position.y = -0.25;
  frame.castShadow = frame.receiveShadow = true;
  scene.add(frame);
  const boardTop = new THREE.Mesh(
    new THREE.PlaneGeometry(8, 8),
    new THREE.MeshStandardMaterial({
      map: getBoardTexture(texSize, renderer.capabilities.getMaxAnisotropy()),
      roughness: 0.38,
      metalness: 0.02,
    }),
  );
  boardTop.rotation.x = -Math.PI / 2;
  boardTop.position.y = 0.002;
  boardTop.receiveShadow = true;
  scene.add(boardTop);
  const floor = new THREE.Mesh(
    new THREE.PlaneGeometry(60, 60),
    new THREE.ShadowMaterial({ opacity: 0.28 }),
  );
  floor.rotation.x = -Math.PI / 2;
  floor.position.y = -0.5;
  floor.receiveShadow = true;
  scene.add(floor);

  // coordinate labels (one set per viewing side so text is never upside-down)
  const labelGeo = new THREE.PlaneGeometry(0.4, 0.4).rotateX(-Math.PI / 2);
  const labelTextures: THREE.Texture[] = [];
  const labelSets: Record<PieceColor, THREE.Group> = { w: new THREE.Group(), b: new THREE.Group() };
  for (const side of ["w", "b"] as const) {
    const sgn = side === "w" ? 1 : -1;
    for (let i = 0; i < 8; i++) {
      for (const [txt, x, z] of [
        [FILES[i]!, i - 3.5, 4.35 * sgn],
        [String(i + 1), -4.35 * sgn, 3.5 - i],
      ] as const) {
        const tex = makeLabelTexture(txt);
        labelTextures.push(tex);
        const m = new THREE.Mesh(
          labelGeo,
          new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false }),
        );
        m.position.set(x, 0.004, z);
        m.rotation.y = side === "w" ? 0 : Math.PI;
        labelSets[side].add(m);
      }
    }
    scene.add(labelSets[side]);
  }

  // ---- highlights ----
  const overlay = new THREE.Group();
  scene.add(overlay);
  const planeGeo = new THREE.PlaneGeometry(1, 1).rotateX(-Math.PI / 2);
  const dotGeo = new THREE.CircleGeometry(0.17, 32).rotateX(-Math.PI / 2);
  const ringGeo = new THREE.RingGeometry(0.36, 0.46, 48).rotateX(-Math.PI / 2);
  const glowTex = makeGlowTexture("230,40,40");
  const flat = (color: number, opacity: number, map?: THREE.Texture) =>
    new THREE.MeshBasicMaterial({
      color,
      opacity,
      map: map ?? null,
      transparent: true,
      depthWrite: false,
    });
  const mats = {
    selected: flat(0xf5d142, 0.5),
    last: flat(0xf2d36b, 0.32),
    dot: flat(0x101010, 0.38),
    ring: flat(0xd94141, 0.85),
    hover: flat(0xffffff, 0.12),
    check: flat(0xffffff, 0.8, glowTex),
  };
  const mark = (geo: THREE.BufferGeometry, mat: THREE.Material, sq: string, y: number) => {
    const m = new THREE.Mesh(geo, mat);
    m.position.copy(toPos(sq)).setY(y);
    overlay.add(m);
  };
  const hoverMesh = new THREE.Mesh(planeGeo, mats.hover);
  hoverMesh.position.y = 0.011;
  hoverMesh.visible = false;
  scene.add(hoverMesh);

  // ---- pieces ----
  const entries: Entry[] = [];
  const create = (p: BoardPiece, instant: boolean) => {
    const obj = buildPiece(p.type, p.color);
    const e: Entry = {
      obj,
      type: p.type,
      color: p.color,
      square: p.square,
      target: toPos(p.square),
      scale: instant ? 1 : 0.01,
      scaleGoal: 1,
      leaving: false,
    };
    obj.userData["entry"] = e;
    obj.position.copy(e.target);
    obj.scale.setScalar(e.scale);
    scene.add(obj);
    entries.push(e);
    return e;
  };

  function reconcile(board: BoardPiece[], lastMove: EngineState["lastMove"], instant: boolean) {
    const pool = entries.filter((e) => !e.leaving);
    const take = (pred: (e: Entry) => boolean) => {
      const i = pool.findIndex(pred);
      return i < 0 ? null : pool.splice(i, 1)[0]!;
    };
    const assign = new Map<BoardPiece, Entry>();
    const rest: BoardPiece[] = [];
    for (const p of board) {
      const e = take((x) => x.square === p.square && x.type === p.type && x.color === p.color);
      if (e) assign.set(p, e);
      else rest.push(p);
    }
    rest.sort((a, b) => Number(b.square === lastMove?.to) - Number(a.square === lastMove?.to));
    for (const p of rest) {
      let e = take(
        (x) =>
          x.type === p.type &&
          x.color === p.color &&
          x.square === lastMove?.from &&
          p.square === lastMove?.to,
      );
      if (!e) {
        const t = toPos(p.square);
        let bi = -1;
        let bd = Infinity;
        pool.forEach((x, i) => {
          const d = x.target.distanceToSquared(t);
          if (x.type === p.type && x.color === p.color && d < bd) [bi, bd] = [i, d];
        });
        e = bi >= 0 ? pool.splice(bi, 1)[0]! : null;
      }
      assign.set(p, e ?? create(p, instant));
    }
    for (const e of pool) {
      e.leaving = true;
      e.scaleGoal = 0;
    }
    assign.forEach((e, p) => {
      e.square = p.square;
      e.target.copy(toPos(p.square));
      e.scaleGoal = 1;
      e.leaving = false;
      if (instant) {
        e.obj.position.copy(e.target);
        e.scale = 1;
      }
    });
  }

  // ---- camera goals ----
  let goal: { theta: number; phi: number } | null = null;
  const sph = new THREE.Spherical();
  controls.addEventListener("start", () => (goal = null));
  controls.addEventListener("change", () => (dirty = true));

  // ---- state / update ----
  let state: EngineState | null = null;
  let prevBoard: BoardPiece[] | null = null;
  let onClick: (sq: string) => void = () => {};
  let dirty = true;

  function update(s: EngineState) {
    const first = state === null;
    const prev = state;
    state = s;
    if (s.board !== prevBoard) {
      reconcile(s.board, s.lastMove, first);
      prevBoard = s.board;
    }
    if (first || prev!.orientation !== s.orientation || prev!.perspective !== s.perspective) {
      const g = { theta: s.orientation === "w" ? 0 : Math.PI, phi: s.perspective ? 0.85 : 0.06 };
      if (first) {
        const dist = 15.5;
        camera.position.setFromSpherical(sph.set(dist, g.phi, g.theta));
        controls.update();
      } else goal = g;
    }
    labelSets.w.visible = s.orientation === "w";
    labelSets.b.visible = s.orientation === "b";

    overlay.clear();
    if (s.lastMove) {
      mark(planeGeo, mats.last, s.lastMove.from, 0.006);
      mark(planeGeo, mats.last, s.lastMove.to, 0.006);
    }
    if (s.selected) mark(planeGeo, mats.selected, s.selected, 0.007);
    if (s.checkSquare) mark(planeGeo, mats.check, s.checkSquare, 0.008);
    const occupied = new Set(s.board.map((p) => p.square));
    for (const t of s.legalTargets)
      mark(occupied.has(t) ? ringGeo : dotGeo, occupied.has(t) ? mats.ring : mats.dot, t, 0.01);
    dirty = true;
  }

  // ---- input ----
  const ray = new THREE.Raycaster();
  const ndc = new THREE.Vector2();
  function pick(ev: PointerEvent): string | null {
    const r = canvas.getBoundingClientRect();
    ndc.set(((ev.clientX - r.left) / r.width) * 2 - 1, -((ev.clientY - r.top) / r.height) * 2 + 1);
    ray.setFromCamera(ndc, camera);
    const hit = ray.intersectObjects(
      [boardTop, ...entries.filter((e) => !e.leaving).map((e) => e.obj)],
      true,
    )[0];
    if (!hit) return null;
    for (let o: THREE.Object3D | null = hit.object; o; o = o.parent) {
      const e = o.userData["entry"] as Entry | undefined;
      if (e) return e.square;
    }
    return toSquare(hit.point.x, hit.point.z);
  }
  let down: { x: number; y: number } | null = null;
  const onDown = (e: PointerEvent) => (down = { x: e.clientX, y: e.clientY });
  const onUp = (e: PointerEvent) => {
    const d = down;
    down = null;
    if (!d || !state?.interactive || Math.hypot(e.clientX - d.x, e.clientY - d.y) > 6) return;
    const sq = pick(e);
    if (sq) onClick(sq);
  };
  const onMove = (e: PointerEvent) => {
    if (e.pointerType !== "mouse" || e.buttons !== 0 || !state?.interactive) return;
    const sq = pick(e);
    hoverMesh.visible = !!sq;
    if (sq) hoverMesh.position.copy(toPos(sq)).setY(0.011);
    canvas.style.cursor = sq ? "pointer" : "grab";
    dirty = true;
  };
  const onLeave = () => {
    hoverMesh.visible = false;
    dirty = true;
  };
  canvas.addEventListener("pointerdown", onDown);
  canvas.addEventListener("pointerup", onUp);
  canvas.addEventListener("pointermove", onMove);
  canvas.addEventListener("pointerleave", onLeave);

  // ---- resize + loop ----
  const resize = () => {
    const w = host.clientWidth || 1;
    const h = host.clientHeight || 1;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
    dirty = true;
  };
  const ro = new ResizeObserver(resize);
  ro.observe(host);
  resize();

  let raf = 0;
  let last = performance.now();
  const tick = (now: number) => {
    raf = requestAnimationFrame(tick);
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    const k = 1 - Math.exp(-dt * 12);

    if (goal) {
      sph.setFromVector3(camera.position.clone().sub(controls.target));
      const dTheta = wrapPi(goal.theta - sph.theta);
      const dPhi = goal.phi - sph.phi;
      const kc = 1 - Math.exp(-dt * 5);
      sph.theta += dTheta * kc;
      sph.phi += dPhi * kc;
      camera.position.setFromSpherical(sph).add(controls.target);
      if (Math.abs(dTheta) < 0.003 && Math.abs(dPhi) < 0.003) goal = null;
      dirty = true;
    }
    if (controls.update()) dirty = true;

    for (let i = entries.length - 1; i >= 0; i--) {
      const e = entries[i]!;
      const dx = e.target.x - e.obj.position.x;
      const dz = e.target.z - e.obj.position.z;
      const dist = Math.hypot(dx, dz);
      const ds = e.scaleGoal - e.scale;
      if (dist > 0.0005 || Math.abs(ds) > 0.001 || e.obj.position.y > 0.0005) {
        e.obj.position.x += dx * k;
        e.obj.position.z += dz * k;
        e.obj.position.y = Math.min(0.6, dist * 0.2) * (e.type === "n" ? 1.6 : 1);
        e.scale += ds * k;
        e.obj.scale.setScalar(Math.max(e.scale, 0.001));
        dirty = true;
      } else {
        e.obj.position.copy(e.target);
        e.obj.scale.setScalar(Math.max(e.scaleGoal, 0.001));
      }
      if (e.leaving && e.scale < 0.03) {
        scene.remove(e.obj);
        entries.splice(i, 1);
        dirty = true;
      }
    }

    const pulsing = !!state?.checkSquare;
    if (pulsing) mats.check.opacity = 0.55 + 0.4 * Math.sin(now / 280);
    if (dirty || pulsing) {
      renderer.render(scene, camera);
      dirty = false;
    }
  };
  raf = requestAnimationFrame(tick);

  return {
    update,
    setOnSquareClick: (fn) => (onClick = fn),
    dispose() {
      cancelAnimationFrame(raf);
      ro.disconnect();
      canvas.removeEventListener("pointerdown", onDown);
      canvas.removeEventListener("pointerup", onUp);
      canvas.removeEventListener("pointermove", onMove);
      canvas.removeEventListener("pointerleave", onLeave);
      controls.dispose();
      labelTextures.forEach((t) => t.dispose());
      [
        planeGeo,
        dotGeo,
        ringGeo,
        labelGeo,
        frame.geometry,
        boardTop.geometry,
        floor.geometry,
      ].forEach((g) => g.dispose());
      Object.values(mats).forEach((m) => m.dispose());
      glowTex.dispose();
      envTex.dispose();
      pmrem.dispose();
      renderer.dispose();
      canvas.remove();
    },
  };
}
