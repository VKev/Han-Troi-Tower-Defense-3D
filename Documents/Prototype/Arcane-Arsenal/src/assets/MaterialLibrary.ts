import * as THREE from 'three';
import { ELEMENT_COLORS, type Element } from '../game/definitions';

export class MaterialLibrary {
  readonly bodyPrimary = new THREE.MeshPhysicalMaterial({
    color: 0x274054,
    metalness: 0.18,
    roughness: 0.46,
    clearcoat: 0.55,
    clearcoatRoughness: 0.22,
  });

  readonly bodySecondary = new THREE.MeshStandardMaterial({
    color: 0x162838,
    metalness: 0.38,
    roughness: 0.52,
  });

  readonly trim = new THREE.MeshStandardMaterial({
    color: 0xe6bd62,
    metalness: 0.72,
    roughness: 0.31,
  });

  readonly groundContact = new THREE.MeshStandardMaterial({
    color: 0x17252d,
    metalness: 0.02,
    roughness: 0.92,
  });

  readonly hazard = new THREE.MeshStandardMaterial({
    color: 0xf05f5b,
    emissive: 0x5b1118,
    emissiveIntensity: 0.52,
    metalness: 0.08,
    roughness: 0.48,
  });

  readonly reward = new THREE.MeshPhysicalMaterial({
    color: 0xffdf72,
    emissive: 0x9b601c,
    emissiveIntensity: 0.72,
    metalness: 0.45,
    roughness: 0.25,
    clearcoat: 0.7,
  });

  readonly shieldBoost = new THREE.MeshPhysicalMaterial({
    color: 0x78d8ff,
    emissive: 0x146ba0,
    emissiveIntensity: 0.75,
    transparent: true,
    opacity: 0.55,
    depthWrite: false,
    roughness: 0.18,
    metalness: 0,
    clearcoat: 1,
  });

  readonly glass = new THREE.MeshPhysicalMaterial({
    color: 0xb5ebff,
    transparent: true,
    opacity: 0.34,
    depthWrite: false,
    roughness: 0.08,
    metalness: 0,
    clearcoat: 1,
  });

  readonly decalDark = new THREE.MeshBasicMaterial({ color: 0x0a1720 });
  readonly decalLight = new THREE.MeshBasicMaterial({ color: 0xffe7a5 });
  readonly path = new THREE.MeshStandardMaterial({ color: 0x7e6048, roughness: 0.9, metalness: 0.02 });
  readonly pathTrim = new THREE.MeshStandardMaterial({ color: 0xe0a95b, roughness: 0.65, metalness: 0.12 });
  readonly grass = new THREE.MeshStandardMaterial({ color: 0x2f765f, roughness: 0.94, metalness: 0 });
  readonly rock = new THREE.MeshStandardMaterial({ color: 0x617078, roughness: 0.88, metalness: 0.04 });
  readonly void = new THREE.MeshStandardMaterial({ color: 0x161337, roughness: 0.7, metalness: 0.08 });

  private readonly elementMaterials = new Map<Element, THREE.MeshStandardMaterial>();
  private readonly enemyMaterials = new Map<number, THREE.MeshStandardMaterial>();

  constructor() {
    for (const [element, color] of Object.entries(ELEMENT_COLORS) as [Element, number][]) {
      this.elementMaterials.set(element, new THREE.MeshStandardMaterial({
        color,
        emissive: color,
        emissiveIntensity: 0.48,
        metalness: 0.08,
        roughness: 0.36,
      }));
    }
  }

  element(element: Element): THREE.MeshStandardMaterial {
    const material = this.elementMaterials.get(element);
    if (!material) throw new Error(`Missing material for ${element}`);
    return material;
  }

  enemy(color: number): THREE.MeshStandardMaterial {
    const existing = this.enemyMaterials.get(color);
    if (existing) return existing;
    const material = new THREE.MeshStandardMaterial({
      color,
      emissive: new THREE.Color(color).multiplyScalar(0.18),
      emissiveIntensity: 0.18,
      roughness: 0.58,
      metalness: 0.06,
    });
    this.enemyMaterials.set(color, material);
    return material;
  }

  projectile(elements: readonly Element[]): THREE.MeshStandardMaterial {
    if (elements.length === 0) {
      return new THREE.MeshStandardMaterial({
        color: 0xffefb8,
        emissive: 0xb58939,
        emissiveIntensity: 1.2,
        roughness: 0.24,
        metalness: 0.18,
      });
    }
    const color = new THREE.Color(0x000000);
    for (const element of elements) color.add(new THREE.Color(ELEMENT_COLORS[element]));
    color.multiplyScalar(1 / elements.length);
    return new THREE.MeshStandardMaterial({
      color,
      emissive: color,
      emissiveIntensity: 1.35,
      roughness: 0.22,
      metalness: 0.08,
    });
  }

  dispose(): void {
    const shared = [
      this.bodyPrimary, this.bodySecondary, this.trim, this.groundContact, this.hazard,
      this.reward, this.shieldBoost, this.glass, this.decalDark, this.decalLight,
      this.path, this.pathTrim, this.grass, this.rock, this.void,
    ];
    for (const material of shared) material.dispose();
    for (const material of this.elementMaterials.values()) material.dispose();
    for (const material of this.enemyMaterials.values()) material.dispose();
  }
}
