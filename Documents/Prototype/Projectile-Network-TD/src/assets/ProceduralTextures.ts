import * as THREE from 'three';

function hash(index: number, salt: number): number {
  const value = Math.sin(index * 91.733 + salt * 37.719) * 43758.5453;
  return value - Math.floor(value);
}

function canvasTexture(
  size: number,
  repeatX: number,
  draw: (context: CanvasRenderingContext2D, size: number) => void,
  repeatY = repeatX,
): THREE.CanvasTexture {
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('Canvas 2D is required for procedural drought textures.');
  draw(context, size);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = THREE.RepeatWrapping;
  texture.wrapT = THREE.RepeatWrapping;
  texture.repeat.set(repeatX, repeatY);
  texture.minFilter = THREE.LinearMipmapLinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.anisotropy = 4;
  return texture;
}

export function createCrackedEarthTexture(): THREE.CanvasTexture {
  return canvasTexture(256, 9, (context, size) => {
    context.fillStyle = '#d8873f';
    context.fillRect(0, 0, size, size);

    for (let index = 0; index < 120; index += 1) {
      const x = hash(index, 1) * size;
      const y = hash(index, 2) * size;
      const radius = 2 + hash(index, 3) * 8;
      context.fillStyle = hash(index, 4) > 0.5 ? 'rgba(255,205,112,.07)' : 'rgba(94,49,25,.055)';
      context.beginPath();
      context.arc(x, y, radius, 0, Math.PI * 2);
      context.fill();
    }

    context.lineCap = 'round';
    context.lineJoin = 'round';
    for (let crack = 0; crack < 34; crack += 1) {
      let x = hash(crack, 8) * size;
      let y = hash(crack, 9) * size;
      const heading = hash(crack, 10) * Math.PI * 2;
      context.beginPath();
      context.moveTo(x, y);
      for (let segment = 0; segment < 4; segment += 1) {
        const angle = heading + (hash(crack * 7 + segment, 11) - 0.5) * 1.5;
        const length = 7 + hash(crack * 5 + segment, 12) * 15;
        x += Math.cos(angle) * length;
        y += Math.sin(angle) * length;
        context.lineTo(x, y);
      }
      context.strokeStyle = `rgba(83,43,24,${0.28 + hash(crack, 13) * 0.22})`;
      context.lineWidth = 0.9 + hash(crack, 14) * 1.4;
      context.stroke();
    }
  });
}

export function createDustPathTexture(): THREE.CanvasTexture {
  return canvasTexture(256, 1, (context, size) => {
    context.fillStyle = '#bd8750';
    context.fillRect(0, 0, size, size);

    // Broad compacted-soil patches keep the path from reading as one flat fill.
    for (let index = 0; index < 110; index += 1) {
      const shade = hash(index, 20) > 0.48 ? 'rgba(246,199,120,.12)' : 'rgba(87,48,25,.11)';
      context.fillStyle = shade;
      const radius = 1.2 + hash(index, 21) * 5.4;
      context.beginPath();
      context.arc(hash(index, 22) * size, hash(index, 23) * size, radius, 0, Math.PI * 2);
      context.fill();
    }

    // Two longitudinal ruts make the ribbon read as a travelled footpath.
    for (const x of [size * 0.29, size * 0.71]) {
      context.strokeStyle = 'rgba(76,42,23,.28)';
      context.lineWidth = 7;
      context.beginPath();
      context.moveTo(x, 0);
      context.bezierCurveTo(x - 5, size * 0.32, x + 6, size * 0.68, x - 2, size);
      context.stroke();
      context.strokeStyle = 'rgba(244,201,127,.1)';
      context.lineWidth = 2;
      context.stroke();
    }

    // Alternating small heel/toe marks suggest a dry trail without becoming a grid.
    for (let step = 0; step < 10; step += 1) {
      const side = step % 2 === 0 ? -1 : 1;
      const x = size * 0.5 + side * (16 + hash(step, 30) * 7);
      const y = (step + 0.4) / 10 * size;
      context.save();
      context.translate(x, y);
      context.rotate(side * (0.12 + hash(step, 31) * 0.18));
      context.fillStyle = 'rgba(72,39,22,.24)';
      context.beginPath();
      context.ellipse(0, 0, 4.4, 8.2, 0, 0, Math.PI * 2);
      context.fill();
      context.restore();
    }

    // Edge scuffs and embedded pebbles break the perfectly clean ribbon silhouette.
    for (let index = 0; index < 42; index += 1) {
      const left = hash(index, 40) > 0.5;
      const x = left ? hash(index, 41) * size * 0.16 : size * (0.84 + hash(index, 42) * 0.16);
      const y = hash(index, 43) * size;
      context.fillStyle = hash(index, 44) > 0.52 ? 'rgba(87,51,31,.38)' : 'rgba(224,169,95,.3)';
      context.beginPath();
      context.ellipse(x, y, 1.2 + hash(index, 45) * 2.8, 0.8 + hash(index, 46) * 1.7, hash(index, 47) * Math.PI, 0, Math.PI * 2);
      context.fill();
    }
  }, 1);
}

export function createProjectileGlowTexture(): THREE.CanvasTexture {
  const size = 96;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const context = canvas.getContext('2d');
  if (!context) throw new Error('Canvas 2D is required for projectile glow textures.');
  const center = size * 0.5;
  const gradient = context.createRadialGradient(center, center, 0, center, center, center);
  gradient.addColorStop(0, 'rgba(255,255,255,1)');
  gradient.addColorStop(0.14, 'rgba(255,255,255,1)');
  gradient.addColorStop(0.34, 'rgba(255,255,255,.72)');
  gradient.addColorStop(0.68, 'rgba(255,255,255,.2)');
  gradient.addColorStop(1, 'rgba(255,255,255,0)');
  context.fillStyle = gradient;
  context.fillRect(0, 0, size, size);
  const texture = new THREE.CanvasTexture(canvas);
  texture.colorSpace = THREE.SRGBColorSpace;
  texture.wrapS = THREE.ClampToEdgeWrapping;
  texture.wrapT = THREE.ClampToEdgeWrapping;
  texture.minFilter = THREE.LinearFilter;
  texture.magFilter = THREE.LinearFilter;
  texture.generateMipmaps = false;
  texture.userData.shared = true;
  return texture;
}
