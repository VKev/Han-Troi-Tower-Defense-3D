import * as THREE from 'three';
import { mergeGeometries } from 'three/examples/jsm/utils/BufferGeometryUtils.js';
import {
  ENEMY_DEFINITIONS, NODE_DEFINITIONS, REACTIONS,
  type Element, type EnemyKind, type NodeType, type Payload,
} from '../game/definitions';
import { MaterialLibrary } from './MaterialLibrary';

function shadows(root: THREE.Object3D): void {
  let casterCount = 0;
  root.traverse((object) => {
    if (!(object instanceof THREE.Mesh)) return;
    object.castShadow = casterCount < 5;
    object.receiveShadow = true;
    casterCount += 1;
  });
}

function runeShape(points = 6, outer = 0.7, inner = 0.48): THREE.ShapeGeometry {
  const shape = new THREE.Shape();
  for (let index = 0; index < points * 2; index += 1) {
    const angle = index / (points * 2) * Math.PI * 2;
    const radius = index % 2 === 0 ? outer : inner;
    const x = Math.cos(angle) * radius;
    const y = Math.sin(angle) * radius;
    if (index === 0) shape.moveTo(x, y); else shape.lineTo(x, y);
  }
  shape.closePath();
  return new THREE.ShapeGeometry(shape);
}

function mergeRepeatedGeometry(
  geometry: THREE.BufferGeometry,
  count: number,
  configure: (object: THREE.Object3D, index: number) => void,
): THREE.BufferGeometry {
  const parts: THREE.BufferGeometry[] = [];
  for (let index = 0; index < count; index += 1) {
    const transform = new THREE.Object3D();
    configure(transform, index);
    transform.updateMatrix();
    const part = geometry.clone();
    part.applyMatrix4(transform.matrix);
    parts.push(part);
  }
  const merged = mergeGeometries(parts, false);
  parts.forEach((part) => part.dispose());
  geometry.dispose();
  if (!merged) throw new Error('Unable to merge repeated procedural geometry.');
  return merged;
}

export class ArtFactory {
  private readonly baseGeometry = new THREE.CylinderGeometry(0.9, 1.06, 0.46, 10);
  private readonly haloGeometry = new THREE.TorusGeometry(0.78, 0.08, 7, 32);

  constructor(readonly materials: MaterialLibrary) {}

  createNode(type: NodeType): THREE.Group {
    if (type === 'nexus') return this.createNexus();
    const definition = NODE_DEFINITIONS[type];
    const root = new THREE.Group();
    root.name = `node-${type}`;
    const shadow = new THREE.Mesh(new THREE.CircleGeometry(1.05, 24), this.materials.shadow);
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.02;
    root.add(shadow);
    const base = new THREE.Mesh(this.baseGeometry, this.materials.obsidian);
    base.name = 'collisionProxy';
    base.position.y = 0.24;
    root.add(base);
    const trim = new THREE.Mesh(this.haloGeometry, this.materials.brass);
    trim.rotation.x = Math.PI / 2;
    trim.position.y = 0.48;
    root.add(trim);
    const teeth = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.13, 0.48, 5), 4, (tooth, index) => {
      const angle = index / 4 * Math.PI * 2;
      tooth.position.set(Math.cos(angle) * 0.84, 0.68, Math.sin(angle) * 0.84);
      tooth.rotation.z = Math.PI;
    }), this.materials.bone);
    root.add(teeth);
    if (type === 'generator') this.decorateGenerator(root);
    else if (definition.element) this.decorateElement(root, definition.element);
    else if (type === 'support') this.decorateSupport(root);
    else this.decorateSpecial(root);
    const port = new THREE.Mesh(new THREE.SphereGeometry(0.13, 8, 6), this.materials.soul);
    port.name = 'outputPort';
    port.position.set(0.88, 1.2, 0);
    root.add(port);
    shadows(root);
    return root;
  }

  createNexus(): THREE.Group {
    const root = new THREE.Group();
    root.name = 'soul-anchor-tower';
    const dais = new THREE.Mesh(new THREE.CylinderGeometry(1.5, 1.78, 0.62, 12), this.materials.obsidian);
    dais.position.y = 0.31;
    root.add(dais);
    const seal = new THREE.Mesh(runeShape(8, 1.28, 1.02), this.materials.soulGlass);
    seal.rotation.x = -Math.PI / 2;
    seal.position.y = 0.65;
    root.add(seal);
    const ribs = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.22, 2.2, 5), 5, (rib, index) => {
      const angle = index / 5 * Math.PI * 2;
      rib.position.set(Math.cos(angle) * 1.0, 1.35, Math.sin(angle) * 1.0);
      rib.rotation.z = Math.cos(angle) * 0.22;
    }), this.materials.bone);
    root.add(ribs);
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.72, 2), this.materials.soul);
    core.name = 'nexusCore';
    core.position.y = 1.75;
    root.add(core);
    const halo = new THREE.Mesh(new THREE.TorusGeometry(1.05, 0.08, 8, 40), this.materials.soulGlass);
    halo.name = 'spinner';
    halo.position.y = 1.75;
    halo.rotation.x = Math.PI / 2;
    root.add(halo);
    shadows(root);
    return root;
  }

  createBaseNexus(): THREE.Group {
    const root = new THREE.Group();
    root.name = 'base-nexus';

    const shadow = new THREE.Mesh(new THREE.CircleGeometry(1.9, 32), this.materials.shadow);
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.02;
    root.add(shadow);

    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(1.42, 1.78, 0.7, 12), this.materials.obsidian);
    pedestal.name = 'collisionProxy';
    pedestal.position.y = 0.35;
    root.add(pedestal);

    const seal = new THREE.Mesh(runeShape(8, 1.34, 1.05), this.materials.soulGlass);
    seal.rotation.x = -Math.PI / 2;
    seal.position.y = 0.72;
    root.add(seal);

    const pillars = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.2, 2.45, 6), 4, (pillar, index) => {
      const angle = index / 4 * Math.PI * 2 + Math.PI / 4;
      pillar.position.set(Math.cos(angle) * 1.15, 1.48, Math.sin(angle) * 1.15);
      pillar.rotation.z = Math.cos(angle) * 0.17;
    }), this.materials.bone);
    root.add(pillars);

    const arch = new THREE.Mesh(new THREE.TorusGeometry(1.18, 0.15, 7, 40, Math.PI), this.materials.brass);
    arch.position.y = 1.55;
    arch.rotation.z = Math.PI / 2;
    root.add(arch);

    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.7, 2), this.materials.soul);
    core.name = 'baseNexusCore';
    core.position.y = 1.72;
    root.add(core);

    const halo = new THREE.Mesh(new THREE.TorusGeometry(1.08, 0.08, 8, 40), this.materials.soulGlass);
    halo.name = 'baseNexusHalo';
    halo.position.y = 1.72;
    halo.rotation.x = Math.PI / 2;
    root.add(halo);

    const crown = new THREE.Mesh(runeShape(6, 0.62, 0.36), this.materials.brass);
    crown.name = 'spinner';
    crown.position.y = 2.7;
    crown.rotation.x = -Math.PI / 2;
    root.add(crown);
    shadows(root);
    return root;
  }

  createEnemy(kind: EnemyKind): THREE.Group {
    const definition = ENEMY_DEFINITIONS[kind];
    const root = new THREE.Group();
    root.name = `enemy-${kind}`;
    const bodyMaterial = this.materials.enemy(definition.color).clone();
    bodyMaterial.userData.baseColor = bodyMaterial.color.clone();
    bodyMaterial.userData.baseEmissive = bodyMaterial.emissive.clone();
    bodyMaterial.userData.baseEmissiveIntensity = bodyMaterial.emissiveIntensity;
    const scale = kind === 'boss' ? 1.42 : kind === 'bulwark' || kind === 'skyWarder' ? 1.14 : 1;
    const flying = kind === 'wisp' || kind === 'skyWarder';
    const body = new THREE.Mesh(
      kind === 'runner' ? new THREE.CapsuleGeometry(0.34, 0.74, 5, 8)
        : flying ? new THREE.OctahedronGeometry(kind === 'skyWarder' ? 0.78 : 0.58, 1)
          : new THREE.DodecahedronGeometry(kind === 'swarm' ? 0.48 : 0.72, 0),
      bodyMaterial,
    );
    body.name = 'body';
    body.position.y = flying ? 0.68 : kind === 'swarm' ? 0.5 : 0.78;
    if (kind === 'runner') body.rotation.z = Math.PI / 2;
    body.scale.set(scale * 1.08, scale, scale * 0.9);
    root.add(body);
    const face = new THREE.Mesh(new THREE.OctahedronGeometry(kind === 'boss' ? 0.28 : 0.18, 0), this.materials.soul);
    face.position.set(0.55 * scale, 0.82 * scale, 0);
    root.add(face);
    const hornCount = kind === 'swarm' ? 2 : flying ? 3 : kind === 'boss' ? 8 : 4;
    const horns = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.08 * scale, 0.42 * scale, 5), hornCount, (horn, index) => {
      const angle = index / hornCount * Math.PI * 2;
      horn.position.set(Math.cos(angle) * 0.56 * scale, 0.88 * scale, Math.sin(angle) * 0.56 * scale);
      horn.rotation.z = Math.PI / 2;
      horn.rotation.y = -angle;
    }), kind === 'armored' || kind === 'bulwark' ? this.materials.brass : this.materials.bone);
    root.add(horns);
    if (kind === 'armored' || kind === 'bulwark') this.addArmor(root, scale);
    if (flying) {
      const wingGeometry = mergeRepeatedGeometry(new THREE.ConeGeometry(0.2, 0.9, 4), 2, (wing, index) => {
        wing.position.set(0, 0.68, index === 0 ? -0.72 : 0.72);
        wing.rotation.x = index === 0 ? Math.PI / 2 : -Math.PI / 2;
      });
      root.add(new THREE.Mesh(wingGeometry, kind === 'skyWarder' ? this.materials.brass : this.materials.soulGlass));
    }
    if (kind === 'warded' || kind === 'bulwark' || kind === 'skyWarder' || kind === 'boss') {
      const ward = new THREE.Mesh(new THREE.SphereGeometry((kind === 'boss' ? 1.1 : 0.86), 14, 10), this.materials.soulGlass.clone());
      ward.name = 'ward';
      ward.position.y = kind === 'boss' ? 1.15 : 0.82;
      root.add(ward);
    }
    if (kind === 'boss') {
      const crown = new THREE.Mesh(new THREE.TorusGeometry(0.82, 0.11, 7, 30), this.materials.brass);
      crown.name = 'spinner';
      crown.rotation.x = Math.PI / 2;
      crown.position.y = 2.08;
      root.add(crown);
    }
    root.userData.bodyMaterial = bodyMaterial;
    shadows(root);
    return root;
  }

  createProjectile(payload: Payload): THREE.Group {
    const root = new THREE.Group();
    const elements = payload.baseElement ? [payload.baseElement] : [];
    const reactionColor = payload.reaction ? REACTIONS[payload.reaction].color : undefined;
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(payload.reaction ? 0.28 : 0.2, 1), this.materials.projectile(elements, reactionColor));
    core.name = 'projectileCore';
    root.add(core);
    if (payload.baseElement) {
      const orbit = new THREE.Mesh(new THREE.TorusGeometry(0.29, 0.025, 5, 20), this.materials.element(payload.baseElement));
      orbit.name = 'spinner';
      orbit.rotation.set(0.7, 0.35, 0.2);
      root.add(orbit);
    }
    if (payload.reaction) {
      for (let index = 0; index < 3; index += 1) {
        const shard = new THREE.Mesh(new THREE.ConeGeometry(0.07, 0.44, 5), this.materials.projectile(elements, reactionColor));
        const angle = index / 3 * Math.PI * 2;
        shard.position.set(Math.cos(angle) * 0.34, 0, Math.sin(angle) * 0.34);
        shard.rotation.z = Math.PI / 2;
        shard.rotation.y = -angle;
        root.add(shard);
      }
    }
    return root;
  }

  createSoulField(branch: 'base' | 'suppression' | 'conduction', radius: number): THREE.Group {
    const root = new THREE.Group();
    const color = branch === 'conduction' ? 0xff9b68 : branch === 'suppression' ? 0x7455dc : 0x9d7cff;
    const disc = new THREE.Mesh(
      new THREE.CircleGeometry(radius, 48),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.2, depthWrite: false, side: THREE.DoubleSide }),
    );
    disc.rotation.x = -Math.PI / 2;
    root.add(disc);
    const ring = new THREE.Mesh(
      new THREE.TorusGeometry(radius, 0.08, 6, 64),
      new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.78, depthWrite: false }),
    );
    ring.rotation.x = Math.PI / 2;
    root.add(ring);
    return root;
  }

  dispose(): void {
    this.baseGeometry.dispose();
    this.haloGeometry.dispose();
  }

  private decorateGenerator(root: THREE.Group): void {
    const well = new THREE.Mesh(new THREE.CylinderGeometry(0.58, 0.76, 1.2, 8, 1, true), this.materials.stoneLight);
    well.position.y = 1.02;
    root.add(well);
    const soul = new THREE.Mesh(new THREE.CapsuleGeometry(0.25, 0.72, 6, 10), this.materials.soulGlass);
    soul.name = 'chargeCore';
    soul.position.y = 1.42;
    root.add(soul);
    const crown = new THREE.Mesh(runeShape(6, 0.72, 0.48), this.materials.brass);
    crown.name = 'spinner';
    crown.rotation.x = -Math.PI / 2;
    crown.position.y = 1.98;
    root.add(crown);
  }

  private decorateElement(root: THREE.Group, element: Element): void {
    const column = new THREE.Mesh(new THREE.CylinderGeometry(0.46, 0.67, 1.18, 7), this.materials.stoneLight);
    column.position.y = 1.02;
    root.add(column);
    const rune = new THREE.Mesh(runeShape(element === 'earth' ? 5 : 6, 0.7, 0.38), this.materials.element(element));
    rune.name = 'spinner';
    rune.rotation.x = -Math.PI / 2;
    rune.position.y = 1.45;
    root.add(rune);
    const geometry = element === 'fire' ? new THREE.OctahedronGeometry(0.42, 0)
      : element === 'ice' ? new THREE.ConeGeometry(0.38, 0.9, 5)
        : element === 'wind' ? new THREE.TorusKnotGeometry(0.25, 0.07, 42, 7)
          : new THREE.DodecahedronGeometry(0.4, 0);
    const core = new THREE.Mesh(geometry, this.materials.element(element));
    core.name = 'elementCore';
    core.position.y = 2.03;
    root.add(core);
  }

  private decorateSupport(root: THREE.Group): void {
    const bowl = new THREE.Mesh(new THREE.CylinderGeometry(0.72, 0.5, 1.12, 8), this.materials.stoneLight);
    bowl.position.y = 1.02;
    root.add(bowl);
    const battery = new THREE.Mesh(new THREE.CapsuleGeometry(0.34, 0.56, 6, 10), this.materials.soulGlass);
    battery.name = 'chargeCore';
    battery.position.y = 1.68;
    root.add(battery);
    const orbit = new THREE.Mesh(new THREE.TorusKnotGeometry(0.42, 0.05, 56, 7), this.materials.soul);
    orbit.name = 'spinner';
    orbit.position.y = 1.68;
    root.add(orbit);
  }

  private decorateSpecial(root: THREE.Group): void {
    const bell = new THREE.Mesh(new THREE.ConeGeometry(0.72, 1.35, 8, 1, true), this.materials.stoneLight);
    bell.position.y = 1.23;
    root.add(bell);
    const heart = new THREE.Mesh(new THREE.IcosahedronGeometry(0.34, 1), this.materials.projectile([], 0xff9f65));
    heart.name = 'pulseCore';
    heart.position.y = 1.56;
    root.add(heart);
    for (let index = 0; index < 6; index += 1) {
      const rib = new THREE.Mesh(new THREE.ConeGeometry(0.08, 0.72, 5), this.materials.bone);
      const angle = index / 6 * Math.PI * 2;
      rib.position.set(Math.cos(angle) * 0.6, 1.05, Math.sin(angle) * 0.6);
      rib.rotation.z = Math.PI / 2;
      rib.rotation.y = -angle;
      root.add(rib);
    }
  }

  private addArmor(root: THREE.Group, scale: number): void {
    for (let index = 0; index < 6; index += 1) {
      const angle = index / 6 * Math.PI * 2;
      const plate = new THREE.Mesh(new THREE.BoxGeometry(0.48 * scale, 0.68 * scale, 0.14), this.materials.brass);
      plate.position.set(Math.cos(angle) * 0.7 * scale, 0.8 * scale, Math.sin(angle) * 0.7 * scale);
      plate.rotation.y = -angle + Math.PI / 2;
      root.add(plate);
    }
  }
}
