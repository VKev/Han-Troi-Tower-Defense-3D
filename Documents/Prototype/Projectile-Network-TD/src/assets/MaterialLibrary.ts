import * as THREE from 'three';
import { ELEMENT_COLORS, type Element } from '../game/definitions';
import { createCrackedEarthTexture, createDustPathTexture, createProjectileGlowTexture } from './ProceduralTextures';

function standard(color: number, emissive = 0x000000, intensity = 0, roughness = 0.72): THREE.MeshStandardMaterial {
  return new THREE.MeshStandardMaterial({ color, emissive, emissiveIntensity: intensity, roughness, metalness: 0.04 });
}

export class MaterialLibrary {
  readonly crackedEarthTexture = createCrackedEarthTexture();
  readonly dustPathTexture = createDustPathTexture();
  readonly projectileGlowTexture = createProjectileGlowTexture();
  readonly stone = standard(0x5d4a37, 0x2a190e, 0.08, 0.92);
  readonly stoneLight = standard(0x8a6945, 0x3b2412, 0.1, 0.86);
  readonly bone = standard(0xd8bb78, 0x6a431d, 0.12, 0.78);
  readonly brass = standard(0xb9812f, 0x6c3a0b, 0.38, 0.46);
  readonly obsidian = standard(0x3a281d, 0x1d1009, 0.18, 0.66);
  readonly terracotta = standard(0xa74f2d, 0x4d1d10, 0.16, 0.72);
  readonly dryWood = standard(0x5a351d, 0x241208, 0.08, 0.96);
  readonly witheredGrass = standard(0xb28b3f, 0x5b3a13, 0.08, 0.92);
  readonly outerDecoration = new THREE.MeshStandardMaterial({
    color: 0xffffff, emissive: 0x241208, emissiveIntensity: 0.08,
    roughness: 0.94, metalness: 0.02, vertexColors: true,
  });
  readonly danger = standard(0xd84a2e, 0x9c2114, 0.9, 0.34);
  readonly ward = new THREE.MeshStandardMaterial({
    color: 0xf2b15f, emissive: 0xa94c1c, emissiveIntensity: 0.55,
    roughness: 0.24, metalness: 0.02, transparent: true, opacity: 0.44, depthWrite: false,
  });
  readonly frogSkin = standard(0x8edbe2, 0x2d7b88, 0.22, 0.56);
  readonly frogBelly = standard(0xcceccf, 0x5e9a82, 0.16, 0.68);
  readonly frogEye = standard(0xffe9b0, 0x8c6428, 0.18, 0.42);
  readonly frogPupil = standard(0x171311, 0x090604, 0.02, 0.62);
  readonly path = new THREE.MeshStandardMaterial({ color: 0xffffff, map: this.dustPathTexture, roughness: 0.96, metalness: 0 });
  readonly pathShoulder = standard(0x7d4b2d, 0x2e160b, 0.05, 1);
  readonly pathEdge = standard(0xa76c3d, 0x4a2614, 0.08, 0.98);
  readonly pathRune = standard(0xf2d083, 0xa76520, 0.34, 0.66);
  readonly ground = new THREE.MeshStandardMaterial({ color: 0xffffff, map: this.crackedEarthTexture, roughness: 0.98, metalness: 0 });
  readonly highGround = new THREE.MeshStandardMaterial({ color: 0xffffff, map: this.crackedEarthTexture, roughness: 0.98, metalness: 0 });
  readonly void = standard(0x563c2c, 0x24170f, 0.08, 1);
  readonly link = new THREE.MeshBasicMaterial({ color: 0x69d8ee, transparent: true, opacity: 0.54, depthWrite: false });
  readonly valid = new THREE.MeshBasicMaterial({ color: 0x73f0b4, transparent: true, opacity: 0.52, depthWrite: false });
  readonly invalid = new THREE.MeshBasicMaterial({ color: 0xff5f69, transparent: true, opacity: 0.48, depthWrite: false });
  readonly soul = standard(0x69d8ee, 0x229db8, 1.25, 0.24);
  readonly soulGlass = new THREE.MeshStandardMaterial({
    color: 0xa8eaf0, emissive: 0x329bb4, emissiveIntensity: 0.78,
    roughness: 0.18, metalness: 0.02, transparent: true, opacity: 0.62, depthWrite: false,
  });
  readonly shadow = new THREE.MeshBasicMaterial({ color: 0x2b180f, transparent: true, opacity: 0.28, depthWrite: false });

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

  projectileGlow(elements: readonly Element[], reactionColor?: number, opacity = 1): THREE.SpriteMaterial {
    let color = new THREE.Color(0xe8dcae);
    if (reactionColor !== undefined) color = new THREE.Color(reactionColor);
    else if (elements.length > 0) color = elements.reduce((sum, element) => sum.add(new THREE.Color(ELEMENT_COLORS[element])), new THREE.Color(0)).multiplyScalar(1 / elements.length);
    return new THREE.SpriteMaterial({
      color,
      map: this.projectileGlowTexture,
      transparent: true,
      opacity,
      blending: THREE.AdditiveBlending,
      depthWrite: false,
      sizeAttenuation: true,
      toneMapped: false,
    });
  }

  dispose(): void {
    [this.stone, this.stoneLight, this.bone, this.brass, this.obsidian, this.path, this.pathShoulder, this.pathEdge, this.pathRune,
      this.terracotta, this.dryWood, this.witheredGrass, this.outerDecoration, this.danger, this.ward,
      this.frogSkin, this.frogBelly, this.frogEye, this.frogPupil,
      this.ground, this.highGround, this.void, this.link, this.valid, this.invalid, this.soul,
      this.soulGlass, this.shadow].forEach((material) => material.dispose());
    this.crackedEarthTexture.dispose();
    this.dustPathTexture.dispose();
    this.projectileGlowTexture.dispose();
    this.elementCache.forEach((material) => material.dispose());
    this.enemyCache.forEach((material) => material.dispose());
  }
}
