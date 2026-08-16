import * as THREE from 'three';
import { ENEMY_DEFINITIONS, TOWER_DEFINITIONS, type EnemyKind, type TowerType } from '../game/definitions';
import { MaterialLibrary } from './MaterialLibrary';

function enableShadows(root: THREE.Object3D): void {
  let hasCaster = false;
  root.traverse((child) => {
    if (!(child instanceof THREE.Mesh)) return;
    child.castShadow = !hasCaster;
    child.receiveShadow = true;
    hasCaster = true;
  });
}

function gearGeometry(teeth: number, innerRadius: number, outerRadius: number, depth: number): THREE.ExtrudeGeometry {
  const shape = new THREE.Shape();
  const steps = teeth * 2;
  for (let index = 0; index < steps; index += 1) {
    const angle = (index / steps) * Math.PI * 2;
    const radius = index % 2 === 0 ? outerRadius : innerRadius;
    const x = Math.cos(angle) * radius;
    const y = Math.sin(angle) * radius;
    if (index === 0) shape.moveTo(x, y);
    else shape.lineTo(x, y);
  }
  shape.closePath();
  const hole = new THREE.Path();
  hole.absarc(0, 0, innerRadius * 0.38, 0, Math.PI * 2, false);
  shape.holes.push(hole);
  const geometry = new THREE.ExtrudeGeometry(shape, {
    depth,
    bevelEnabled: true,
    bevelSegments: 1,
    bevelSize: 0.04,
    bevelThickness: 0.04,
    curveSegments: 2,
  });
  geometry.center();
  return geometry;
}

function crystalGeometry(radius = 0.42, height = 1.2): THREE.ConeGeometry {
  return new THREE.ConeGeometry(radius, height, 5, 1);
}

export class ArtFactory {
  private readonly baseGeometry = new THREE.CylinderGeometry(0.78, 0.92, 0.42, 10);
  private readonly ringGeometry = new THREE.TorusGeometry(0.67, 0.09, 8, 28);
  private readonly gear = gearGeometry(12, 0.48, 0.68, 0.12);
  private readonly smallGear = gearGeometry(10, 0.25, 0.38, 0.09);

  constructor(readonly materials: MaterialLibrary) {}

  createTower(type: TowerType): THREE.Group {
    const definition = TOWER_DEFINITIONS[type];
    const root = new THREE.Group();
    root.name = `tower-${type}`;

    const base = new THREE.Mesh(this.baseGeometry, this.materials.bodySecondary);
    base.name = 'collisionProxy';
    base.position.y = 0.21;
    root.add(base);

    const trimRing = new THREE.Mesh(this.ringGeometry, this.materials.trim);
    trimRing.rotation.x = Math.PI / 2;
    trimRing.position.y = 0.43;
    root.add(trimRing);

    if (type === 'foundry') this.decorateFoundry(root);
    else if (definition.element) this.decorateElement(root, definition.element);
    else if (type === 'amplifier') this.decorateAmplifier(root);
    else this.decorateExplosion(root);

    enableShadows(root);
    return root;
  }

  createEnemy(kind: EnemyKind): THREE.Group {
    const definition = ENEMY_DEFINITIONS[kind];
    const root = new THREE.Group();
    root.name = `enemy-${kind}`;
    const bodyMaterial = this.materials.enemy(definition.color).clone();
    bodyMaterial.userData.baseEmissiveIntensity = bodyMaterial.emissiveIntensity;
    bodyMaterial.userData.baseColor = bodyMaterial.color.clone();
    bodyMaterial.userData.baseEmissive = bodyMaterial.emissive.clone();

    if (kind === 'riftling') this.decorateRiftling(root, bodyMaterial);
    else if (kind === 'runner') this.decorateRunner(root, bodyMaterial);
    else if (kind === 'brute') this.decorateBrute(root, bodyMaterial);
    else if (kind === 'wisp') this.decorateWisp(root, bodyMaterial);
    else if (kind === 'frostRay') this.decorateFrostRay(root, bodyMaterial);
    else if (kind === 'colossus') this.decorateColossus(root, bodyMaterial);
    else if (kind === 'arcaneBulwark') this.decorateArcaneBulwark(root, bodyMaterial);
    else if (kind === 'skyWarder') this.decorateSkyWarder(root, bodyMaterial);
    else this.decorateWarder(root, bodyMaterial);

    root.userData.bodyMaterial = bodyMaterial;
    enableShadows(root);
    return root;
  }

  createNexus(): THREE.Group {
    const root = new THREE.Group();
    root.name = 'arcane-nexus';
    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(1.45, 1.75, 0.65, 12), this.materials.bodySecondary);
    pedestal.position.y = 0.32;
    root.add(pedestal);

    for (let index = 0; index < 4; index += 1) {
      const angle = (index / 4) * Math.PI * 2 + Math.PI / 4;
      const pillar = new THREE.Mesh(new THREE.CylinderGeometry(0.18, 0.28, 2.1, 6), this.materials.trim);
      pillar.position.set(Math.cos(angle) * 1.1, 1.35, Math.sin(angle) * 1.1);
      pillar.rotation.z = Math.cos(angle) * 0.16;
      root.add(pillar);
    }

    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.72, 1), this.materials.shieldBoost);
    core.name = 'nexusCore';
    core.position.y = 1.62;
    root.add(core);

    const halo = new THREE.Mesh(new THREE.TorusGeometry(1.12, 0.08, 8, 40), this.materials.reward);
    halo.name = 'nexusHalo';
    halo.position.y = 1.62;
    halo.rotation.x = Math.PI / 2;
    root.add(halo);
    enableShadows(root);
    return root;
  }

  createWall(width: number, depth: number, height: number): THREE.Group {
    const root = new THREE.Group();
    const slab = new THREE.Mesh(new THREE.BoxGeometry(width, height, depth), this.materials.rock);
    slab.position.y = height / 2;
    root.add(slab);
    const count = Math.max(2, Math.floor(width / 0.8));
    for (let index = 0; index < count; index += 1) {
      const crystal = new THREE.Mesh(crystalGeometry(0.24, 0.9), index % 2 === 0 ? this.materials.element('ice') : this.materials.element('earth'));
      crystal.position.set(-width / 2 + 0.45 + index * (width - 0.9) / Math.max(1, count - 1), height + 0.35, 0);
      crystal.rotation.z = (index % 2 === 0 ? -1 : 1) * 0.12;
      root.add(crystal);
    }
    enableShadows(root);
    return root;
  }

  createProjectile(elements: readonly ('fire' | 'ice' | 'wind' | 'earth')[]): THREE.Group {
    const root = new THREE.Group();
    const material = this.materials.projectile(elements);
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.18 + elements.length * 0.025, 1), material);
    core.name = 'projectileCore';
    root.add(core);
    for (let index = 0; index < elements.length; index += 1) {
      const orbit = new THREE.Mesh(new THREE.TorusGeometry(0.25 + index * 0.035, 0.022, 5, 18), this.materials.element(elements[index]));
      orbit.rotation.set(index * 0.8, index * 0.6, index * 0.45);
      root.add(orbit);
    }
    return root;
  }

  dispose(): void {
    this.baseGeometry.dispose();
    this.ringGeometry.dispose();
    this.gear.dispose();
    this.smallGear.dispose();
  }

  private decorateFoundry(root: THREE.Group): void {
    const tower = new THREE.Mesh(new THREE.CylinderGeometry(0.52, 0.7, 1.35, 8), this.materials.bodyPrimary);
    tower.position.y = 1.02;
    root.add(tower);

    const hopper = new THREE.Mesh(new THREE.CylinderGeometry(0.78, 0.46, 0.58, 8, 1, true), this.materials.trim);
    hopper.position.y = 1.83;
    root.add(hopper);

    const gear = new THREE.Mesh(this.gear, this.materials.trim);
    gear.name = 'spinner';
    gear.position.set(0.55, 1.02, 0);
    gear.rotation.y = Math.PI / 2;
    root.add(gear);

    const magazine = new THREE.Mesh(new THREE.CapsuleGeometry(0.22, 0.72, 6, 10), this.materials.glass);
    magazine.name = 'bufferCore';
    magazine.position.set(-0.5, 1.15, 0);
    root.add(magazine);
  }

  private decorateElement(root: THREE.Group, element: 'fire' | 'ice' | 'wind' | 'earth'): void {
    const column = new THREE.Mesh(new THREE.CylinderGeometry(0.43, 0.62, 1.25, 7), this.materials.bodyPrimary);
    column.position.y = 1.02;
    root.add(column);

    const outerGear = new THREE.Mesh(this.gear, this.materials.element(element));
    outerGear.name = 'spinner';
    outerGear.position.y = 1.35;
    outerGear.rotation.x = Math.PI / 2;
    root.add(outerGear);

    const coreGeometry = element === 'earth'
      ? new THREE.DodecahedronGeometry(0.42, 0)
      : element === 'wind'
        ? new THREE.TorusKnotGeometry(0.28, 0.08, 48, 8)
        : element === 'ice'
          ? crystalGeometry(0.38, 0.88)
          : new THREE.OctahedronGeometry(0.42, 0);
    const core = new THREE.Mesh(coreGeometry, this.materials.element(element));
    core.name = 'elementCore';
    core.position.y = 2.04;
    root.add(core);

    const nozzle = new THREE.Mesh(new THREE.CylinderGeometry(0.15, 0.24, 0.78, 8), this.materials.trim);
    nozzle.name = 'aimNozzle';
    nozzle.rotation.z = Math.PI / 2;
    nozzle.position.set(0.62, 1.55, 0);
    root.add(nozzle);
  }

  private decorateAmplifier(root: THREE.Group): void {
    const tower = new THREE.Mesh(new THREE.CylinderGeometry(0.56, 0.76, 1.2, 8), this.materials.bodyPrimary);
    tower.position.y = 0.98;
    root.add(tower);
    const coil = new THREE.Mesh(new THREE.TorusKnotGeometry(0.42, 0.08, 56, 8), this.materials.element('wind'));
    coil.name = 'spinner';
    coil.position.y = 1.75;
    root.add(coil);
    const crystal = new THREE.Mesh(new THREE.OctahedronGeometry(0.38, 0), this.materials.shieldBoost);
    crystal.name = 'amplifierCore';
    crystal.position.y = 1.76;
    root.add(crystal);
    for (let index = 0; index < 3; index += 1) {
      const pylon = new THREE.Mesh(new THREE.CylinderGeometry(0.1, 0.16, 1.1, 6), this.materials.trim);
      const angle = index / 3 * Math.PI * 2;
      pylon.position.set(Math.cos(angle) * 0.72, 1.15, Math.sin(angle) * 0.72);
      pylon.rotation.z = Math.cos(angle) * 0.2;
      root.add(pylon);
    }
  }

  private decorateExplosion(root: THREE.Group): void {
    const chassis = new THREE.Mesh(new THREE.CylinderGeometry(0.82, 0.96, 0.62, 10), this.materials.bodyPrimary);
    chassis.position.y = 0.72;
    root.add(chassis);
    const reactorRing = new THREE.Mesh(new THREE.TorusGeometry(0.7, 0.12, 7, 24), this.materials.trim);
    reactorRing.name = 'spinner';
    reactorRing.rotation.x = Math.PI / 2;
    reactorRing.position.y = 1.08;
    root.add(reactorRing);
    const chamber = new THREE.Mesh(new THREE.OctahedronGeometry(0.48, 1), this.materials.glass);
    chamber.name = 'bufferCore';
    chamber.position.y = 1.36;
    root.add(chamber);
    const pressureCrown = new THREE.Mesh(new THREE.TorusGeometry(0.48, 0.07, 6, 20), this.materials.reward);
    pressureCrown.name = 'blastCore';
    pressureCrown.rotation.x = Math.PI / 2;
    pressureCrown.position.y = 1.66;
    root.add(pressureCrown);
    for (let index = 0; index < 8; index += 1) {
      const angle = index / 8 * Math.PI * 2;
      const vent = new THREE.Mesh(new THREE.ConeGeometry(0.13, 0.52, 5), index % 2 === 0 ? this.materials.reward : this.materials.trim);
      vent.name = 'blastVent';
      vent.position.set(Math.cos(angle) * 0.82, 0.92, Math.sin(angle) * 0.82);
      vent.rotation.z = Math.PI / 2;
      vent.rotation.y = -angle;
      root.add(vent);
    }
  }

  private decorateRiftling(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const body = new THREE.Mesh(new THREE.SphereGeometry(0.48, 10, 7), material);
    body.scale.set(1.25, 0.75, 1);
    body.position.y = 0.42;
    root.add(body);
    const head = new THREE.Mesh(new THREE.DodecahedronGeometry(0.3, 0), this.materials.hazard);
    head.position.set(0.52, 0.48, 0);
    root.add(head);
    for (const side of [-1, 1]) {
      for (const z of [-0.3, 0, 0.3]) {
        const leg = new THREE.Mesh(new THREE.CylinderGeometry(0.045, 0.06, 0.58, 5), this.materials.bodySecondary);
        leg.name = 'leg';
        leg.position.set(0, 0.22, z);
        leg.rotation.z = side * 0.98;
        root.add(leg);
      }
    }
  }

  private decorateRunner(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const body = new THREE.Mesh(new THREE.CapsuleGeometry(0.32, 0.72, 6, 10), material);
    body.rotation.z = Math.PI / 2;
    body.position.y = 0.52;
    root.add(body);
    const nose = new THREE.Mesh(new THREE.ConeGeometry(0.28, 0.7, 5), this.materials.hazard);
    nose.rotation.z = -Math.PI / 2;
    nose.position.set(0.72, 0.52, 0);
    root.add(nose);
    for (const z of [-0.38, 0.38]) {
      const blade = new THREE.Mesh(new THREE.BoxGeometry(0.74, 0.08, 0.18), this.materials.trim);
      blade.position.set(-0.05, 0.38, z);
      blade.rotation.y = z * 0.4;
      root.add(blade);
    }
  }

  private decorateBrute(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const body = new THREE.Mesh(new THREE.DodecahedronGeometry(0.78, 0), material);
    body.scale.set(1.1, 1.25, 0.95);
    body.position.y = 0.85;
    root.add(body);
    for (let index = 0; index < 5; index += 1) {
      const plate = new THREE.Mesh(new THREE.BoxGeometry(0.62, 0.16, 0.48), this.materials.rock);
      plate.position.set(-0.28 + index * 0.14, 0.88 + index * 0.2, 0);
      plate.rotation.z = -0.2 + index * 0.1;
      root.add(plate);
    }
    const horn = new THREE.Mesh(new THREE.ConeGeometry(0.16, 0.7, 6), this.materials.trim);
    horn.rotation.z = -Math.PI / 2;
    horn.position.set(0.88, 1.05, 0);
    root.add(horn);
  }

  private decorateWisp(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.5, 1), material);
    core.name = 'hoverCore';
    root.add(core);
    const halo = new THREE.Mesh(new THREE.TorusGeometry(0.72, 0.07, 6, 28), this.materials.element('fire'));
    halo.name = 'spinner';
    halo.rotation.x = Math.PI / 2;
    root.add(halo);
    for (let index = 0; index < 3; index += 1) {
      const tail = new THREE.Mesh(crystalGeometry(0.14, 0.7), this.materials.element('fire'));
      tail.name = 'tail';
      tail.position.set(-0.55, -0.14 + index * 0.16, (index - 1) * 0.22);
      tail.rotation.z = Math.PI / 2;
      root.add(tail);
    }
  }

  private decorateFrostRay(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const shape = new THREE.Shape();
    shape.moveTo(0.85, 0);
    shape.lineTo(-0.1, 0.72);
    shape.lineTo(-0.82, 0.45);
    shape.lineTo(-0.45, 0);
    shape.lineTo(-0.82, -0.45);
    shape.lineTo(-0.1, -0.72);
    shape.closePath();
    const body = new THREE.Mesh(new THREE.ExtrudeGeometry(shape, { depth: 0.2, bevelEnabled: true, bevelSize: 0.08, bevelThickness: 0.08, bevelSegments: 1 }), material);
    body.name = 'rayBody';
    body.rotation.x = Math.PI / 2;
    body.position.y = -0.1;
    root.add(body);
    const core = new THREE.Mesh(crystalGeometry(0.25, 0.7), this.materials.element('ice'));
    core.rotation.z = -Math.PI / 2;
    core.position.set(0.22, 0.12, 0);
    root.add(core);
  }

  private decorateWarder(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const body = new THREE.Mesh(new THREE.DodecahedronGeometry(0.84, 1), material);
    body.scale.set(0.9, 1.25, 0.9);
    body.position.y = 1.02;
    root.add(body);
    const crown = new THREE.Mesh(new THREE.TorusGeometry(0.62, 0.11, 8, 32), this.materials.reward);
    crown.name = 'spinner';
    crown.position.y = 1.7;
    crown.rotation.x = Math.PI / 2;
    root.add(crown);
    const shield = new THREE.Mesh(new THREE.SphereGeometry(1.08, 18, 12), this.materials.shieldBoost.clone());
    shield.name = 'barrier';
    shield.position.y = 1.02;
    root.add(shield);
    for (let index = 0; index < 4; index += 1) {
      const fin = new THREE.Mesh(new THREE.BoxGeometry(0.18, 0.72, 0.38), this.materials.trim);
      const angle = index / 4 * Math.PI * 2;
      fin.position.set(Math.cos(angle) * 0.78, 1.04, Math.sin(angle) * 0.78);
      fin.rotation.y = -angle;
      root.add(fin);
    }
  }

  private decorateArcaneBulwark(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const body = new THREE.Mesh(new THREE.DodecahedronGeometry(0.76, 1), material);
    body.scale.set(1.08, 1.24, 0.94);
    body.position.y = 0.96;
    root.add(body);

    const armorShell = new THREE.Group();
    armorShell.name = 'armorShell';
    for (let index = 0; index < 6; index += 1) {
      const angle = index / 6 * Math.PI * 2;
      const plate = new THREE.Mesh(new THREE.BoxGeometry(0.52, 0.78, 0.14), this.materials.trim);
      plate.position.set(Math.cos(angle) * 0.76, 0.98, Math.sin(angle) * 0.76);
      plate.rotation.y = -angle + Math.PI / 2;
      plate.rotation.z = index % 2 === 0 ? 0.1 : -0.1;
      armorShell.add(plate);
    }
    const fireLock = new THREE.Mesh(new THREE.OctahedronGeometry(0.2, 0), this.materials.element('fire'));
    fireLock.position.set(0.48, 1.62, -0.34);
    armorShell.add(fireLock);
    const iceLock = new THREE.Mesh(crystalGeometry(0.18, 0.48), this.materials.element('ice'));
    iceLock.position.set(0.48, 1.62, 0.34);
    armorShell.add(iceLock);
    const barrier = new THREE.Mesh(new THREE.SphereGeometry(1.13, 18, 12), this.materials.shieldBoost.clone());
    barrier.name = 'barrier';
    barrier.position.y = 0.98;
    armorShell.add(barrier);
    root.add(armorShell);

    const ram = new THREE.Mesh(new THREE.ConeGeometry(0.2, 0.84, 6), this.materials.hazard);
    ram.rotation.z = -Math.PI / 2;
    ram.position.set(0.98, 1.08, 0);
    root.add(ram);
  }

  private decorateSkyWarder(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    const core = new THREE.Mesh(new THREE.OctahedronGeometry(0.68, 1), material);
    core.name = 'hoverCore';
    core.scale.set(1.18, 0.82, 1.18);
    root.add(core);
    const shield = new THREE.Mesh(new THREE.SphereGeometry(1.02, 18, 12), this.materials.shieldBoost.clone());
    shield.name = 'barrier';
    root.add(shield);
    const halo = new THREE.Mesh(new THREE.TorusGeometry(0.86, 0.1, 8, 32), this.materials.element('wind'));
    halo.name = 'spinner';
    halo.rotation.x = Math.PI / 2;
    root.add(halo);
    for (const side of [-1, 1]) {
      const wing = new THREE.Mesh(crystalGeometry(0.28, 1.05), this.materials.element('earth'));
      wing.position.set(0, -0.06, side * 0.92);
      wing.rotation.x = side * Math.PI / 2;
      root.add(wing);
    }
  }

  private decorateColossus(root: THREE.Group, material: THREE.MeshStandardMaterial): void {
    this.decorateBrute(root, material);
    root.scale.setScalar(1.34);
    const crown = new THREE.Mesh(new THREE.TorusGeometry(0.86, 0.13, 8, 32), this.materials.reward);
    crown.name = 'spinner';
    crown.position.y = 1.72;
    crown.rotation.x = Math.PI / 2;
    root.add(crown);
    const shield = new THREE.Mesh(new THREE.SphereGeometry(1.12, 18, 12), this.materials.shieldBoost.clone());
    shield.name = 'barrier';
    shield.position.y = 0.92;
    root.add(shield);
  }
}
