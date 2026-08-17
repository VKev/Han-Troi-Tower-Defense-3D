import * as THREE from 'three';
import { ELEMENT_COLORS, type Element } from '../game/definitions';

function standard(color: number, emissive = 0x000000, intensity = 0, roughness = 0.72): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({ color, emissive, emissiveIntensity: intensity, roughness, metalness: 0.04 });
}

export class MaterialLibrary {
  readonly stone = standard(0x2a2839, 0x171224, 0.14, 0.88);
  readonly stoneLight = standard(0x4e465d, 0x251f34, 0.12, 0.82);
  readonly bone = standard(0xd6c7a5, 0x5c4d34, 0.16, 0.74);
  readonly brass = standard(0xaa8750, 0x5d3f18, 0.34, 0.5);
  readonly obsidian = standard(0x141321, 0x10081b, 0.28, 0.54);
  readonly path = standard(0x51465c, 0x22172c, 0.12, 0.94);
  readonly pathRune = standard(0x9e7cc2, 0x6a3a94, 0.72, 0.58);
  readonly ground = standard(0x253b37, 0x102922, 0.12, 0.92);
  readonly highGround = standard(0x334a43, 0x16332b, 0.15, 0.88);
  readonly void = standard(0x0b0b15, 0x080511, 0.18, 1);
  readonly link = new THREE.MeshBasicMaterial({ color: 0xa986e8, transparent: true, opacity: 0.48, depthWrite: false });
  readonly valid = new THREE.MeshBasicMaterial({ color: 0x73f0b4, transparent: true, opacity: 0.52, depthWrite: false });
  readonly invalid = new THREE.MeshBasicMaterial({ color: 0xff5f69, transparent: true, opacity: 0.48, depthWrite: false });
  readonly soul = standard(0xa986ff, 0x7b4dcc, 1.3, 0.24);
  readonly soulGlass = new THREE.MeshStandardMaterial({
    color: 0xb9a0ff, emissive: 0x7c50d2, emissiveIntensity: 0.85,
    roughness: 0.18, metalness: 0.02, transparent: true, opacity: 0.62, depthWrite: false,
  });
  readonly shadow = new THREE.MeshBasicMaterial({ color: 0x05050a, transparent: true, opacity: 0.3, depthWrite: false });

  private readonly elementCache = new Map<Element, THREE.MeshStandardMaterial>();
  private readonly enemyCache = new Map<number, THREE.MeshStandardMaterial>();

  element(element: Element): THREE.MeshStandardMaterial {
    const cached = this.elementCache.get(element);
    if (cached) return cached;
    const color = ELEMENT_COLORS[element];
    const material = standard(color, color, 1.1, 0.28);
    this.elementCache.set(element, material);
    return material;
  }

  enemy(color: number): THREE.MeshStandardMaterial {
    const cached = this.enemyCache.get(color);
    if (cached) return cached;
    const material = standard(color, color, 0.12, 0.72);
    this.enemyCache.set(color, material);
    return material;
  }

  projectile(elements: readonly Element[], reactionColor?: number): THREE.MeshStandardMaterial {
    let color = new THREE.Color(0xe8dcae);
    if (reactionColor !== undefined) color = new THREE.Color(reactionColor);
    else if (elements.length > 0) color = elements.reduce((sum, element) => sum.add(new THREE.Color(ELEMENT_COLORS[element])), new THREE.Color(0)).multiplyScalar(1 / elements.length);
    return new THREE.MeshStandardMaterial({ color, emissive: color, emissiveIntensity: reactionColor ? 2.2 : 1.45, roughness: 0.15, metalness: 0.05 });
  }

  dispose(): void {
    [this.stone, this.stoneLight, this.bone, this.brass, this.obsidian, this.path, this.pathRune,
      this.ground, this.highGround, this.void, this.link, this.valid, this.invalid, this.soul,
      this.soulGlass, this.shadow].forEach((material) => material.dispose());
    this.elementCache.forEach((material) => material.dispose());
    this.enemyCache.forEach((material) => material.dispose());
  }
}
