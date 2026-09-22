/**
 * Dedicated canvas handler: rAF render loop, pointer gestures, hit-testing.
 * Blazor pushes scene snapshots; commits flow back via DotNetObjectReference.
 */
export function createCanvasController(canvas, dotNetRef) {
  const ctx = canvas.getContext("2d");
  const state = {
    parts: [],
    selectedPartIds: new Set(),
    selectedEntityIds: new Set(),
    panX: 0,
    panY: 0,
    zoom: 1,
    dirty: true,
    raf: 0,
    spaceDown: false,
    interaction: null, // { type, ... }
    lastPointerScreen: null, // { x, y } while pointer is over canvas
    cssWidth: 0,
    cssHeight: 0,
    dpr: 1,
  };

  const HIT_PX = 6;
  const HANDLE_OFFSET = 34;
  const HANDLE_SIZE = 22;
  const HANDLE_HIT = 14;

  const rotateIcon = new Image();
  rotateIcon.src = "img/rotate.svg";
  rotateIcon.onload = () => invalidate();

  function invalidate() {
    state.dirty = true;
    if (!state.raf) {
      state.raf = requestAnimationFrame(frame);
    }
  }

  function frame() {
    state.raf = 0;
    if (!state.dirty && !state.interaction) {
      return;
    }
    state.dirty = false;
    draw();
    if (state.interaction) {
      state.raf = requestAnimationFrame(frame);
    }
  }

  function resize() {
    const rect = canvas.getBoundingClientRect();
    state.cssWidth = rect.width;
    state.cssHeight = rect.height;
    state.dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.floor(rect.width * state.dpr));
    canvas.height = Math.max(1, Math.floor(rect.height * state.dpr));
    ctx.setTransform(state.dpr, 0, 0, state.dpr, 0, 0);
    invalidate();
  }

  function worldToScreen(x, y) {
    return {
      x: x * state.zoom + state.panX,
      y: -y * state.zoom + state.panY,
    };
  }

  function screenToWorld(x, y) {
    return {
      x: (x - state.panX) / state.zoom,
      y: -(y - state.panY) / state.zoom,
    };
  }

  function transformLocal(part, x, y, overlay) {
    let lx = x;
    let ly = y;
    const mirrored = overlay?.mirrored ?? part.mirrored;
    const rot = overlay?.rotationDegrees ?? part.rotationDegrees;
    const ox = overlay?.offsetX ?? part.offsetX;
    const oy = overlay?.offsetY ?? part.offsetY;
    if (mirrored) lx = -lx;
    const rad = (rot * Math.PI) / 180;
    const c = Math.cos(rad);
    const s = Math.sin(rad);
    const rx = lx * c - ly * s;
    const ry = lx * s + ly * c;
    return { x: rx + ox, y: ry + oy };
  }

  function getTransientOverlay(partId) {
    const i = state.interaction;
    if (!i) return null;
    if (i.type === "move" && i.ids.has(partId)) {
      return {
        offsetX: i.base[partId].offsetX + i.dx,
        offsetY: i.base[partId].offsetY + i.dy,
        rotationDegrees: i.base[partId].rotationDegrees,
        mirrored: i.base[partId].mirrored,
      };
    }
    if (i.type === "rotate" && i.ids.has(partId)) {
      const b = i.base[partId];
      const rad = (i.deltaDeg * Math.PI) / 180;
      const cos = Math.cos(rad);
      const sin = Math.sin(rad);
      const dx = b.offsetX - i.centroid.x;
      const dy = b.offsetY - i.centroid.y;
      return {
        offsetX: i.centroid.x + dx * cos - dy * sin,
        offsetY: i.centroid.y + dx * sin + dy * cos,
        rotationDegrees: b.rotationDegrees + i.deltaDeg,
        mirrored: b.mirrored,
      };
    }
    return null;
  }

  /** Parent blocks to move/rotate/chrome when parts or entities are selected. */
  function getEditablePartIds() {
    if (state.selectedPartIds.size > 0) {
      return new Set(state.selectedPartIds);
    }
    const ids = new Set();
    for (const part of state.parts) {
      for (const ent of part.entities) {
        if (state.selectedEntityIds.has(ent.id)) {
          ids.add(part.id);
          break;
        }
      }
    }
    return ids;
  }

  function hasSelection() {
    return state.selectedPartIds.size > 0 || state.selectedEntityIds.size > 0;
  }

  function partWorldCorners(part, overlay) {
    const corners = [
      [part.localMinX, part.localMinY],
      [part.localMaxX, part.localMinY],
      [part.localMaxX, part.localMaxY],
      [part.localMinX, part.localMaxY],
    ];
    return corners.map(([x, y]) => transformLocal(part, x, y, overlay));
  }

  function partWorldBounds(part, overlay) {
    const pts = partWorldCorners(part, overlay);
    let minX = Infinity,
      minY = Infinity,
      maxX = -Infinity,
      maxY = -Infinity;
    for (const p of pts) {
      minX = Math.min(minX, p.x);
      minY = Math.min(minY, p.y);
      maxX = Math.max(maxX, p.x);
      maxY = Math.max(maxY, p.y);
    }
    return { minX, minY, maxX, maxY };
  }

  function selectionWorldBounds() {
    const editable = getEditablePartIds();
    let minX = Infinity,
      minY = Infinity,
      maxX = -Infinity,
      maxY = -Infinity;
    let any = false;
    for (const part of state.parts) {
      if (!editable.has(part.id)) continue;
      any = true;
      const b = partWorldBounds(part, getTransientOverlay(part.id));
      minX = Math.min(minX, b.minX);
      minY = Math.min(minY, b.minY);
      maxX = Math.max(maxX, b.maxX);
      maxY = Math.max(maxY, b.maxY);
    }
    return any ? { minX, minY, maxX, maxY } : null;
  }

  function workspaceWorldBounds() {
    if (state.parts.length === 0) return null;
    let minX = Infinity,
      minY = Infinity,
      maxX = -Infinity,
      maxY = -Infinity;
    for (const part of state.parts) {
      const b = partWorldBounds(part, getTransientOverlay(part.id));
      minX = Math.min(minX, b.minX);
      minY = Math.min(minY, b.minY);
      maxX = Math.max(maxX, b.maxX);
      maxY = Math.max(maxY, b.maxY);
    }
    return { minX, minY, maxX, maxY };
  }

  function selectionCentroid() {
    const b = selectionWorldBounds();
    if (!b) return null;
    return { x: (b.minX + b.maxX) * 0.5, y: (b.minY + b.maxY) * 0.5 };
  }

  function rotateHandleWorld() {
    const b = selectionWorldBounds();
    if (!b) return null;
    const cx = (b.minX + b.maxX) * 0.5;
    const top = b.maxY;
    return { x: cx, y: top + HANDLE_OFFSET / state.zoom };
  }

  function distPointSeg(px, py, x1, y1, x2, y2) {
    const dx = x2 - x1;
    const dy = y2 - y1;
    const len2 = dx * dx + dy * dy;
    if (len2 < 1e-18) {
      const ex = px - x1;
      const ey = py - y1;
      return Math.sqrt(ex * ex + ey * ey);
    }
    let t = ((px - x1) * dx + (py - y1) * dy) / len2;
    t = Math.max(0, Math.min(1, t));
    const qx = x1 + t * dx;
    const qy = y1 + t * dy;
    const ex = px - qx;
    const ey = py - qy;
    return Math.sqrt(ex * ex + ey * ey);
  }

  /** @returns {{ partId: string, entityId: string } | null} */
  function hitTestEntity(screenX, screenY) {
    const world = screenToWorld(screenX, screenY);
    const thresh = HIT_PX / state.zoom;
    // Top-most last in list wins: iterate reverse.
    for (let i = state.parts.length - 1; i >= 0; i--) {
      const part = state.parts[i];
      const overlay = getTransientOverlay(part.id);
      const b = partWorldBounds(part, overlay);
      if (
        world.x < b.minX - thresh ||
        world.x > b.maxX + thresh ||
        world.y < b.minY - thresh ||
        world.y > b.maxY + thresh
      ) {
        continue;
      }
      for (const ent of part.entities) {
        const flat = ent.polyline;
        for (let j = 0; j + 3 < flat.length; j += 2) {
          const a = transformLocal(part, flat[j], flat[j + 1], overlay);
          const c = transformLocal(part, flat[j + 2], flat[j + 3], overlay);
          if (distPointSeg(world.x, world.y, a.x, a.y, c.x, c.y) <= thresh) {
            return { partId: part.id, entityId: ent.id };
          }
        }
      }
    }
    return null;
  }

  function hitRotateHandle(screenX, screenY) {
    const h = rotateHandleWorld();
    if (!h) return false;
    const s = worldToScreen(h.x, h.y);
    const dx = screenX - s.x;
    const dy = screenY - s.y;
    return dx * dx + dy * dy <= HANDLE_HIT * HANDLE_HIT;
  }

  function updateHoverCursor(screenX, screenY) {
    if (state.interaction) return;
    if (getEditablePartIds().size > 0 && hitRotateHandle(screenX, screenY)) {
      canvas.style.cursor = "grab";
    } else {
      canvas.style.cursor = "";
    }
  }

  function drawGrid() {
    const stepWorld = niceGridStep(80 / state.zoom);
    const topLeft = screenToWorld(0, 0);
    const bottomRight = screenToWorld(state.cssWidth, state.cssHeight);
    const minX = Math.min(topLeft.x, bottomRight.x);
    const maxX = Math.max(topLeft.x, bottomRight.x);
    const minY = Math.min(topLeft.y, bottomRight.y);
    const maxY = Math.max(topLeft.y, bottomRight.y);

    ctx.save();
    ctx.strokeStyle = "rgba(255,255,255,0.06)";
    ctx.lineWidth = 1;
    ctx.beginPath();
    const startX = Math.floor(minX / stepWorld) * stepWorld;
    for (let x = startX; x <= maxX; x += stepWorld) {
      const s = worldToScreen(x, minY);
      const e = worldToScreen(x, maxY);
      ctx.moveTo(s.x, s.y);
      ctx.lineTo(e.x, e.y);
    }
    const startY = Math.floor(minY / stepWorld) * stepWorld;
    for (let y = startY; y <= maxY; y += stepWorld) {
      const s = worldToScreen(minX, y);
      const e = worldToScreen(maxX, y);
      ctx.moveTo(s.x, s.y);
      ctx.lineTo(e.x, e.y);
    }
    ctx.stroke();
    ctx.restore();
  }

  function niceGridStep(raw) {
    const pow = Math.pow(10, Math.floor(Math.log10(raw)));
    const n = raw / pow;
    let f = 1;
    if (n > 5) f = 10;
    else if (n > 2) f = 5;
    else if (n > 1) f = 2;
    return f * pow;
  }

  function drawEntityPath(part, flat, overlay) {
    if (flat.length < 4) return false;
    const p0 = transformLocal(part, flat[0], flat[1], overlay);
    const s0 = worldToScreen(p0.x, p0.y);
    ctx.moveTo(s0.x, s0.y);
    for (let i = 2; i + 1 < flat.length; i += 2) {
      const p = transformLocal(part, flat[i], flat[i + 1], overlay);
      const s = worldToScreen(p.x, p.y);
      ctx.lineTo(s.x, s.y);
    }
    return true;
  }

  function drawPart(part) {
    const overlay = getTransientOverlay(part.id);
    const partSelected = state.selectedPartIds.has(part.id);
    for (const ent of part.entities) {
      const entitySelected = state.selectedEntityIds.has(ent.id);
      const selected = partSelected || entitySelected;
      ctx.beginPath();
      if (!drawEntityPath(part, ent.polyline, overlay)) continue;
      ctx.strokeStyle = ent.colorHex || "#000000";
      ctx.lineWidth = selected ? 2.25 : 1.25;
      ctx.lineCap = "butt";
      ctx.lineJoin = "round";
      // Same dotted highlight for whole-block and single-entity selection.
      ctx.setLineDash(selected ? [3, 5] : []);
      ctx.stroke();
      ctx.setLineDash([]);
    }
  }

  function drawWorkspaceBounds() {
    const b = workspaceWorldBounds();
    if (!b) return;
    const tl = worldToScreen(b.minX, b.maxY);
    const br = worldToScreen(b.maxX, b.minY);
    const x = Math.min(tl.x, br.x);
    const y = Math.min(tl.y, br.y);
    const w = Math.abs(br.x - tl.x);
    const h = Math.abs(br.y - tl.y);

    ctx.save();
    ctx.strokeStyle = "rgba(160,168,180,0.7)";
    ctx.lineWidth = 1;
    ctx.setLineDash([]);
    ctx.strokeRect(x, y, w, h);

    const worldW = b.maxX - b.minX;
    const worldH = b.maxY - b.minY;
    const label = `${worldW.toFixed(1)} × ${worldH.toFixed(1)} mm`;
    const labelX = x + w * 0.5;
    const labelY = y + h + 16;
    ctx.font = "12px Segoe UI, sans-serif";
    ctx.textAlign = "center";
    ctx.textBaseline = "top";
    ctx.fillStyle = "rgba(160,168,180,0.95)";
    ctx.fillText(label, labelX, labelY);
    ctx.restore();
  }

  function drawSelectionChrome() {
    const b = selectionWorldBounds();
    if (!b) return;
    const tl = worldToScreen(b.minX, b.maxY);
    const br = worldToScreen(b.maxX, b.minY);
    const x = Math.min(tl.x, br.x);
    const y = Math.min(tl.y, br.y);
    const w = Math.abs(br.x - tl.x);
    const h = Math.abs(br.y - tl.y);

    ctx.save();
    ctx.strokeStyle = "rgba(240,180,41,0.9)";
    ctx.setLineDash([5, 4]);
    ctx.lineWidth = 1;
    ctx.strokeRect(x, y, w, h);
    ctx.setLineDash([]);

    const handle = rotateHandleWorld();
    if (handle) {
      const hs = worldToScreen(handle.x, handle.y);
      const topMid = worldToScreen((b.minX + b.maxX) * 0.5, b.maxY);
      ctx.beginPath();
      ctx.moveTo(topMid.x, topMid.y);
      ctx.lineTo(hs.x, hs.y);
      ctx.strokeStyle = "rgba(240,180,41,0.8)";
      ctx.stroke();

      ctx.beginPath();
      ctx.arc(hs.x, hs.y, HANDLE_SIZE * 0.55, 0, Math.PI * 2);
      ctx.fillStyle = "#f0b429";
      ctx.fill();
      ctx.strokeStyle = "#1a1d23";
      ctx.lineWidth = 1.5;
      ctx.stroke();

      if (rotateIcon.complete && rotateIcon.naturalWidth > 0) {
        const size = HANDLE_SIZE;
        ctx.drawImage(rotateIcon, hs.x - size * 0.5, hs.y - size * 0.5, size, size);
      }
    }
    ctx.restore();
  }

  function drawMarquee() {
    const i = state.interaction;
    if (!i || i.type !== "marquee") return;
    const x = Math.min(i.x0, i.x1);
    const y = Math.min(i.y0, i.y1);
    const w = Math.abs(i.x1 - i.x0);
    const h = Math.abs(i.y1 - i.y0);
    ctx.save();
    ctx.fillStyle = "rgba(80,160,255,0.12)";
    ctx.strokeStyle = "rgba(80,160,255,0.85)";
    ctx.lineWidth = 1;
    ctx.fillRect(x, y, w, h);
    ctx.strokeRect(x, y, w, h);
    ctx.restore();
  }

  function draw() {
    ctx.clearRect(0, 0, state.cssWidth, state.cssHeight);
    ctx.fillStyle = "#1a1d23";
    ctx.fillRect(0, 0, state.cssWidth, state.cssHeight);
    drawGrid();

    for (const part of state.parts) {
      drawPart(part);
    }
    drawWorkspaceBounds();
    drawSelectionChrome();
    drawMarquee();

    if (state.parts.length === 0) {
      ctx.fillStyle = "rgba(215,221,229,0.45)";
      ctx.font = "15px Segoe UI, sans-serif";
      ctx.textAlign = "center";
      ctx.fillText("Drop .dxf files here", state.cssWidth / 2, state.cssHeight / 2);
    }
  }

  function pointerPos(e) {
    const rect = canvas.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function beginMoveInteraction(partIds, pointerId, screenX, screenY, pendingSingleSelectId = null) {
    const world = screenToWorld(screenX, screenY);
    const base = {};
    const ids = new Set(partIds);
    for (const part of state.parts) {
      if (!ids.has(part.id)) continue;
      base[part.id] = {
        offsetX: part.offsetX,
        offsetY: part.offsetY,
        rotationDegrees: part.rotationDegrees,
        mirrored: part.mirrored,
      };
    }
    state.interaction = {
      type: "move",
      ids,
      base,
      startWorld: world,
      dx: 0,
      dy: 0,
      pointerId,
      moved: false,
      pendingSingleSelectId,
    };
    canvas.setPointerCapture(pointerId);
  }

  function onPointerDown(e) {
    canvas.focus({ preventScroll: true });
    const p = pointerPos(e);
    state.lastPointerScreen = p;
    const isPan =
      e.button === 1 || (e.button === 0 && (state.spaceDown || e.altKey));

    if (isPan) {
      state.interaction = {
        type: "pan",
        lastX: p.x,
        lastY: p.y,
        pointerId: e.pointerId,
      };
      canvas.style.cursor = "grabbing";
      canvas.setPointerCapture(e.pointerId);
      e.preventDefault();
      invalidate();
      return;
    }

    if (e.button !== 0) return;

    const editableIds = getEditablePartIds();
    if (editableIds.size > 0 && hitRotateHandle(p.x, p.y)) {
      const centroid = selectionCentroid();
      const world = screenToWorld(p.x, p.y);
      const startAngle = Math.atan2(world.y - centroid.y, world.x - centroid.x);
      const base = {};
      for (const part of state.parts) {
        if (!editableIds.has(part.id)) continue;
        base[part.id] = {
          offsetX: part.offsetX,
          offsetY: part.offsetY,
          rotationDegrees: part.rotationDegrees,
          mirrored: part.mirrored,
        };
      }
      state.interaction = {
        type: "rotate",
        ids: new Set(editableIds),
        base,
        centroid,
        startAngle,
        deltaDeg: 0,
        pointerId: e.pointerId,
      };
      canvas.style.cursor = "grabbing";
      canvas.setPointerCapture(e.pointerId);
      e.preventDefault();
      invalidate();
      return;
    }

    const hit = hitTestEntity(p.x, p.y);
    if (hit) {
      const ctrl = e.ctrlKey || e.metaKey;

      if (ctrl) {
        // Entity selection mode (mutually exclusive with part selection).
        state.selectedPartIds = new Set();
        if (state.selectedEntityIds.has(hit.entityId)) {
          state.selectedEntityIds.delete(hit.entityId);
        } else {
          state.selectedEntityIds.add(hit.entityId);
        }
        commitSelection();
        const parents = getEditablePartIds();
        if (parents.size > 0) {
          beginMoveInteraction(parents, e.pointerId, p.x, p.y);
        }
      } else if (e.shiftKey) {
        // Block multi-select.
        state.selectedEntityIds = new Set();
        if (state.selectedPartIds.has(hit.partId)) {
          state.selectedPartIds.delete(hit.partId);
        } else {
          state.selectedPartIds.add(hit.partId);
        }
        commitSelection();
        if (state.selectedPartIds.has(hit.partId)) {
          beginMoveInteraction(state.selectedPartIds, e.pointerId, p.x, p.y);
        }
      } else {
        // Plain click: select entire block.
        // Unselected hit → select only that part immediately.
        // Already selected → keep multi-selection for drag; narrow on pointerup if no drag.
        state.selectedEntityIds = new Set();
        let pendingSingleSelectId = null;
        if (!state.selectedPartIds.has(hit.partId)) {
          state.selectedPartIds = new Set([hit.partId]);
          commitSelection();
        } else if (state.selectedPartIds.size > 1) {
          pendingSingleSelectId = hit.partId;
        }
        beginMoveInteraction(
          state.selectedPartIds,
          e.pointerId,
          p.x,
          p.y,
          pendingSingleSelectId
        );
      }

      e.preventDefault();
      invalidate();
      return;
    }

    // Empty space: clear on plain click, then marquee (block-level).
    if (!e.shiftKey) {
      state.selectedPartIds = new Set();
      state.selectedEntityIds = new Set();
      commitSelection();
    }
    state.interaction = {
      type: "marquee",
      x0: p.x,
      y0: p.y,
      x1: p.x,
      y1: p.y,
      shift: e.shiftKey,
      pointerId: e.pointerId,
    };
    canvas.setPointerCapture(e.pointerId);
    e.preventDefault();
    invalidate();
  }

  function onPointerMove(e) {
    const i = state.interaction;
    const p = pointerPos(e);
    state.lastPointerScreen = p;
    if (!i || i.pointerId !== e.pointerId) {
      updateHoverCursor(p.x, p.y);
      return;
    }

    if (i.type === "pan") {
      state.panX += p.x - i.lastX;
      state.panY += p.y - i.lastY;
      i.lastX = p.x;
      i.lastY = p.y;
      invalidate();
      return;
    }

    if (i.type === "move") {
      const world = screenToWorld(p.x, p.y);
      i.dx = world.x - i.startWorld.x;
      i.dy = world.y - i.startWorld.y;
      i.moved = Math.abs(i.dx) + Math.abs(i.dy) > 1e-9;
      invalidate();
      return;
    }

    if (i.type === "rotate") {
      const world = screenToWorld(p.x, p.y);
      const angle = Math.atan2(world.y - i.centroid.y, world.x - i.centroid.x);
      i.deltaDeg = ((angle - i.startAngle) * 180) / Math.PI;
      invalidate();
      return;
    }

    if (i.type === "marquee") {
      i.x1 = p.x;
      i.y1 = p.y;
      invalidate();
    }
  }

  function onPointerLeave(e) {
    state.lastPointerScreen = null;
    if (state.interaction) return;
    canvas.style.cursor = "";
  }

  async function onPointerUp(e) {
    const i = state.interaction;
    if (!i || i.pointerId !== e.pointerId) return;

    const p = pointerPos(e);

    if (i.type === "pan") {
      state.interaction = null;
      updateHoverCursor(p.x, p.y);
      await commitViewport();
      invalidate();
      return;
    }

    if (i.type === "move") {
      if (i.moved) {
        const transforms = [];
        for (const id of i.ids) {
          const b = i.base[id];
          transforms.push({
            id,
            offsetX: b.offsetX + i.dx,
            offsetY: b.offsetY + i.dy,
            rotationDegrees: b.rotationDegrees,
            mirrored: b.mirrored,
          });
          const part = state.parts.find((p) => p.id === id);
          if (part) {
            part.offsetX = b.offsetX + i.dx;
            part.offsetY = b.offsetY + i.dy;
          }
        }
        state.interaction = null;
        updateHoverCursor(p.x, p.y);
        await dotNetRef.invokeMethodAsync("OnPartsTransformed", transforms);
      } else {
        if (i.pendingSingleSelectId != null) {
          state.selectedPartIds = new Set([i.pendingSingleSelectId]);
          state.selectedEntityIds = new Set();
          state.interaction = null;
          updateHoverCursor(p.x, p.y);
          await commitSelection();
        } else {
          state.interaction = null;
          updateHoverCursor(p.x, p.y);
        }
      }
      invalidate();
      return;
    }

    if (i.type === "rotate") {
      const transforms = [];
      const rad = (i.deltaDeg * Math.PI) / 180;
      const cos = Math.cos(rad);
      const sin = Math.sin(rad);
      for (const id of i.ids) {
        const b = i.base[id];
        const dx = b.offsetX - i.centroid.x;
        const dy = b.offsetY - i.centroid.y;
        const nx = i.centroid.x + dx * cos - dy * sin;
        const ny = i.centroid.y + dx * sin + dy * cos;
        const rot = b.rotationDegrees + i.deltaDeg;
        transforms.push({
          id,
          offsetX: nx,
          offsetY: ny,
          rotationDegrees: rot,
          mirrored: b.mirrored,
        });
        const part = state.parts.find((p) => p.id === id);
        if (part) {
          part.offsetX = nx;
          part.offsetY = ny;
          part.rotationDegrees = rot;
        }
      }
      state.interaction = null;
      updateHoverCursor(p.x, p.y);
      await dotNetRef.invokeMethodAsync("OnPartsTransformed", transforms);
      invalidate();
      return;
    }

    if (i.type === "marquee") {
      const x0 = Math.min(i.x0, i.x1);
      const y0 = Math.min(i.y0, i.y1);
      const x1 = Math.max(i.x0, i.x1);
      const y1 = Math.max(i.y0, i.y1);
      const w0 = screenToWorld(x0, y1);
      const w1 = screenToWorld(x1, y0);
      const minX = Math.min(w0.x, w1.x);
      const maxX = Math.max(w0.x, w1.x);
      const minY = Math.min(w0.y, w1.y);
      const maxY = Math.max(w0.y, w1.y);
      const box = { minX, minY, maxX, maxY };
      const hits = [];
      for (const part of state.parts) {
        const b = partWorldBounds(part, null);
        if (
          b.maxX >= box.minX &&
          b.minX <= box.maxX &&
          b.maxY >= box.minY &&
          b.minY <= box.maxY
        ) {
          hits.push(part.id);
        }
      }
      state.selectedEntityIds = new Set();
      if (i.shift) {
        for (const id of hits) state.selectedPartIds.add(id);
      } else {
        state.selectedPartIds = new Set(hits);
      }
      state.interaction = null;
      updateHoverCursor(p.x, p.y);
      await commitSelection();
      invalidate();
    }
  }

  function onWheel(e) {
    e.preventDefault();
    const p = pointerPos(e);
    const before = screenToWorld(p.x, p.y);
    const factor = e.deltaY < 0 ? 1.1 : 1 / 1.1;
    state.zoom = Math.min(50, Math.max(0.05, state.zoom * factor));
    const after = worldToScreen(before.x, before.y);
    state.panX += p.x - after.x;
    state.panY += p.y - after.y;
    invalidate();
    scheduleViewportCommit();
  }

  let viewportTimer = 0;
  function scheduleViewportCommit() {
    clearTimeout(viewportTimer);
    viewportTimer = setTimeout(() => commitViewport(), 120);
  }

  async function commitViewport() {
    try {
      await dotNetRef.invokeMethodAsync("OnViewportChanged", state.panX, state.panY, state.zoom);
    } catch {
      /* disposed */
    }
  }

  async function commitSelection() {
    try {
      await dotNetRef.invokeMethodAsync(
        "OnSelectionChanged",
        Array.from(state.selectedPartIds),
        Array.from(state.selectedEntityIds)
      );
    } catch {
      /* disposed */
    }
  }

  function onKeyDown(e) {
    if (e.code === "Space") {
      state.spaceDown = true;
      e.preventDefault();
      return;
    }

    const tag = (e.target && e.target.tagName) || "";
    if (tag === "INPUT" || tag === "TEXTAREA" || e.target?.isContentEditable) {
      return;
    }

    if (e.key === "Escape") {
      if (document.querySelector('[role="dialog"][aria-modal="true"]')) {
        return;
      }
      if (hasSelection()) {
        state.selectedPartIds = new Set();
        state.selectedEntityIds = new Set();
        commitSelection();
        invalidate();
      }
      e.preventDefault();
      return;
    }

    const mod = e.ctrlKey || e.metaKey;
    if (mod && (e.key === "d" || e.key === "D") && getEditablePartIds().size > 0) {
      e.preventDefault();
      if (state.lastPointerScreen) {
        const w = screenToWorld(state.lastPointerScreen.x, state.lastPointerScreen.y);
        dotNetRef.invokeMethodAsync("OnDuplicateRequested", w.x, w.y);
      } else {
        dotNetRef.invokeMethodAsync("OnDuplicateRequested", null, null);
      }
      return;
    }

    if ((e.key === "Delete" || e.key === "Backspace") && getEditablePartIds().size > 0) {
      e.preventDefault();
      dotNetRef.invokeMethodAsync("OnDeleteRequested");
    }
  }

  function onKeyUp(e) {
    if (e.code === "Space") {
      state.spaceDown = false;
    }
  }

  function onDragOver(e) {
    e.preventDefault();
    e.dataTransfer.dropEffect = "copy";
  }

  async function onDrop(e) {
    e.preventDefault();
    const files = [...(e.dataTransfer?.files || [])].filter((f) =>
      f.name.toLowerCase().endsWith(".dxf")
    );
    if (!files.length) return;

    const payloads = [];
    for (const file of files) {
      const buffer = await file.arrayBuffer();
      const bytes = new Uint8Array(buffer);
      let binary = "";
      const chunk = 0x8000;
      for (let i = 0; i < bytes.length; i += chunk) {
        binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
      }
      payloads.push({ name: file.name, dataBase64: btoa(binary) });
    }
    await dotNetRef.invokeMethodAsync("OnFilesDropped", payloads);
  }

  const ro = new ResizeObserver(() => resize());
  ro.observe(canvas.parentElement || canvas);

  canvas.tabIndex = 0;
  canvas.addEventListener("pointerdown", onPointerDown);
  canvas.addEventListener("pointermove", onPointerMove);
  canvas.addEventListener("pointerup", onPointerUp);
  canvas.addEventListener("pointercancel", onPointerUp);
  canvas.addEventListener("pointerleave", onPointerLeave);
  canvas.addEventListener("wheel", onWheel, { passive: false });
  canvas.addEventListener("dragover", onDragOver);
  canvas.addEventListener("drop", onDrop);
  window.addEventListener("keydown", onKeyDown);
  window.addEventListener("keyup", onKeyUp);

  resize();

  // Center origin roughly in view.
  state.panX = state.cssWidth * 0.5;
  state.panY = state.cssHeight * 0.5;
  invalidate();
  commitViewport();

  return {
    setScene(scene) {
      state.parts = (scene.parts || []).map((p) => ({
        id: p.id,
        name: p.name,
        entities: (p.entities || []).map((ent) => ({
          id: ent.id,
          polyline: ent.polyline || [],
          colorHex: ent.colorHex || null,
        })),
        offsetX: p.offsetX,
        offsetY: p.offsetY,
        rotationDegrees: p.rotationDegrees,
        mirrored: p.mirrored,
        localMinX: p.localMinX,
        localMinY: p.localMinY,
        localMaxX: p.localMaxX,
        localMaxY: p.localMaxY,
      }));
      state.selectedPartIds = new Set(scene.selectedPartIds || []);
      state.selectedEntityIds = new Set(scene.selectedEntityIds || []);
      // Viewport stays JS-authoritative; C# is updated via OnViewportChanged only.
      invalidate();
    },
    dispose() {
      cancelAnimationFrame(state.raf);
      clearTimeout(viewportTimer);
      ro.disconnect();
      canvas.removeEventListener("pointerdown", onPointerDown);
      canvas.removeEventListener("pointermove", onPointerMove);
      canvas.removeEventListener("pointerup", onPointerUp);
      canvas.removeEventListener("pointercancel", onPointerUp);
      canvas.removeEventListener("pointerleave", onPointerLeave);
      canvas.removeEventListener("wheel", onWheel);
      canvas.removeEventListener("dragover", onDragOver);
      canvas.removeEventListener("drop", onDrop);
      window.removeEventListener("keydown", onKeyDown);
      window.removeEventListener("keyup", onKeyUp);
    },
  };
}
