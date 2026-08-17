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
    root.userData.modelProfile = type === 'generator' ? 'foundry-flywheel'
      : type === 'fire' ? 'wide-brazier'
        : type === 'ice' ? 'asymmetric-crystal-crown'
          : type === 'wind' ? 'broad-wind-rotor'
            : type === 'earth' ? 'squat-stepped-monolith'
              : type === 'support' ? 'soul-battery' : 'thunder-bell';
    const shadow = new THREE.Mesh(new THREE.CircleGeometry(1.05, 24), this.materials.shadow);
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.02;
    root.add(shadow);
    const baseGeometry = type === 'generator' ? new THREE.BoxGeometry(1.58, 0.52, 1.18)
      : type === 'fire' ? new THREE.CylinderGeometry(1.02, 1.16, 0.48, 12)
        : type === 'ice' ? new THREE.CylinderGeometry(0.92, 1.08, 0.58, 6)
          : type === 'wind' ? new THREE.CylinderGeometry(0.64, 1.02, 0.62, 3)
            : type === 'earth' ? new THREE.BoxGeometry(1.48, 0.56, 1.28)
              : this.baseGeometry;
    const baseMaterial = type === 'ice' ? this.materials.stoneLight
      : type === 'earth' || type === 'wind' ? this.materials.stone : this.materials.terracotta;
    const base = new THREE.Mesh(baseGeometry, baseMaterial);
    base.name = 'collisionProxy';
    base.position.y = type === 'wind' ? 0.31 : type === 'ice' || type === 'earth' ? 0.29 : 0.26;
    root.add(base);
    if (type === 'support' || type === 'special') {
      const trim = new THREE.Mesh(this.haloGeometry, this.materials.brass);
      trim.rotation.x = Math.PI / 2;
      trim.position.y = 0.48;
      root.add(trim);
      const teeth = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.13, 0.48, 5), 4, (tooth, index) => {
        const angle = index / 4 * Math.PI * 2;
        tooth.position.set(Math.cos(angle) * 0.84, 0.68, Math.sin(angle) * 0.84);
        tooth.rotation.z = Math.PI;
      }), this.materials.stoneLight);
      root.add(teeth);
    } else if (type === 'generator') {
      const railGeometry = mergeRepeatedGeometry(new THREE.BoxGeometry(0.16, 0.12, 1.34), 2, (rail, index) => {
        rail.position.set((index === 0 ? -1 : 1) * 0.64, 0.58, 0);
      });
      const rails = new THREE.Mesh(railGeometry, this.materials.brass);
      rails.name = 'foundryBaseRails';
      root.add(rails);
    } else if (type === 'fire') {
      const rim = new THREE.Mesh(new THREE.TorusGeometry(0.98, 0.08, 6, 36), this.materials.brass);
      rim.name = 'fireBaseRing';
      rim.rotation.x = Math.PI / 2;
      rim.position.y = 0.51;
      root.add(rim);
    } else if (type === 'ice') {
      const seal = new THREE.Mesh(runeShape(6, 0.84, 0.67), this.materials.bone);
      seal.name = 'iceBaseSeal';
      seal.rotation.x = -Math.PI / 2;
      seal.position.y = 0.6;
      root.add(seal);
    } else if (type === 'wind') {
      const braces = new THREE.Mesh(mergeRepeatedGeometry(new THREE.BoxGeometry(0.12, 0.12, 1.22), 3, (brace, index) => {
        brace.position.y = 0.65;
        brace.rotation.y = index / 3 * Math.PI;
      }), this.materials.brass);
      braces.name = 'windTripodBraces';
      root.add(braces);
    } else {
      const cap = new THREE.Mesh(new THREE.BoxGeometry(1.26, 0.12, 1.06), this.materials.brass);
      cap.name = 'earthBaseBand';
      cap.position.y = 0.61;
      root.add(cap);
    }
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
    root.name = 'rain-calling-drum';
    const shadow = new THREE.Mesh(new THREE.CircleGeometry(1.72, 28), this.materials.shadow);
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.02;
    root.add(shadow);
    const dais = new THREE.Mesh(new THREE.CylinderGeometry(1.42, 1.68, 0.54, 12), this.materials.stone);
    dais.position.y = 0.31;
    root.add(dais);
    const seal = new THREE.Mesh(runeShape(8, 1.28, 1.02), this.materials.brass);
    seal.rotation.x = -Math.PI / 2;
    seal.position.y = 0.59;
    root.add(seal);
    [-0.86, 0.86].forEach((z) => {
      const post = new THREE.Mesh(new THREE.BoxGeometry(0.26, 1.45, 0.26), this.materials.dryWood);
      post.position.set(0, 1.22, z);
      root.add(post);
      const foot = new THREE.Mesh(new THREE.BoxGeometry(0.68, 0.2, 0.5), this.materials.stoneLight);
      foot.position.set(0, 0.7, z);
      root.add(foot);
    });
    const drum = new THREE.Mesh(new THREE.CylinderGeometry(0.68, 0.68, 1.48, 16), this.materials.terracotta);
    drum.name = 'rainDrum';
    drum.position.y = 1.55;
    drum.rotation.z = Math.PI / 2;
    root.add(drum);
    [-0.76, 0.76].forEach((x) => {
      const head = new THREE.Mesh(new THREE.CylinderGeometry(0.73, 0.73, 0.1, 16), this.materials.bone);
      head.position.set(x, 1.55, 0);
      head.rotation.z = Math.PI / 2;
      root.add(head);
    });
    [-0.54, -0.18, 0.18, 0.54].forEach((x) => {
      const band = new THREE.Mesh(new THREE.TorusGeometry(0.7, 0.045, 6, 28), this.materials.brass);
      band.position.set(x, 1.55, 0);
      band.rotation.y = Math.PI / 2;
      root.add(band);
    });
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.25, 1), this.materials.soul);
    core.name = 'nexusCore';
    core.position.y = 2.45;
    core.scale.set(0.72, 1.2, 0.72);
    root.add(core);
    const halo = new THREE.Mesh(new THREE.TorusGeometry(1.18, 0.07, 7, 36), this.materials.soulGlass);
    halo.name = 'spinner';
    halo.position.y = 0.68;
    halo.rotation.x = Math.PI / 2;
    root.add(halo);
    shadows(root);
    return root;
  }

  createBaseNexus(): THREE.Group {
    const root = new THREE.Group();
    root.name = 'frog-nexus';

    const shadow = new THREE.Mesh(new THREE.CircleGeometry(1.9, 32), this.materials.shadow);
    shadow.rotation.x = -Math.PI / 2;
    shadow.position.y = 0.02;
    root.add(shadow);

    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(1.5, 1.72, 0.28, 14), this.materials.stone);
    pedestal.name = 'collisionProxy';
    pedestal.position.y = 0.14;
    root.add(pedestal);
    const ripple = new THREE.Mesh(new THREE.TorusGeometry(1.32, 0.07, 7, 40), this.materials.soulGlass);
    ripple.name = 'baseNexusHalo';
    ripple.rotation.x = Math.PI / 2;
    ripple.position.y = 0.31;
    root.add(ripple);

    const actor = new THREE.Group();
    actor.name = 'frogActor';
    root.add(actor);
    const travelShadow = new THREE.Mesh(new THREE.CircleGeometry(1.08, 24), this.materials.shadow);
    travelShadow.name = 'frogTravelShadow';
    travelShadow.rotation.x = -Math.PI / 2;
    travelShadow.position.y = 0.025;
    travelShadow.visible = false;
    actor.add(travelShadow);

    const body = new THREE.Mesh(new THREE.SphereGeometry(0.92, 14, 10), this.materials.frogSkin);
    body.name = 'frogBody';
    body.position.set(0.2, 0.86, 0);
    body.scale.set(1.08, 0.78, 0.92);
    actor.add(body);
    const belly = new THREE.Mesh(new THREE.SphereGeometry(0.6, 12, 8), this.materials.frogBelly);
    belly.position.set(-0.52, 0.79, 0);
    belly.scale.set(0.5, 0.78, 0.82);
    actor.add(belly);
    const head = new THREE.Mesh(new THREE.SphereGeometry(0.82, 14, 10), this.materials.frogSkin);
    head.position.set(-0.58, 1.28, 0);
    head.scale.set(0.94, 0.72, 1.0);
    actor.add(head);
    [-0.4, 0.4].forEach((z) => {
      const eye = new THREE.Mesh(new THREE.SphereGeometry(0.25, 10, 8), this.materials.frogEye);
      eye.position.set(-1.03, 1.62, z);
      actor.add(eye);
      const pupil = new THREE.Mesh(new THREE.SphereGeometry(0.1, 8, 6), this.materials.frogPupil);
      pupil.position.set(-1.25, 1.64, z);
      actor.add(pupil);
      const foreleg = new THREE.Mesh(new THREE.CapsuleGeometry(0.13, 0.52, 4, 7), this.materials.frogSkin);
      foreleg.position.set(-0.52, 0.43, z * 1.35);
      foreleg.rotation.z = Math.PI / 2.8;
      actor.add(foreleg);
    });
    [-0.68, 0.68].forEach((z) => {
      const thigh = new THREE.Mesh(new THREE.SphereGeometry(0.42, 10, 7), this.materials.frogSkin);
      thigh.position.set(0.62, 0.48, z);
      thigh.scale.set(1.15, 0.58, 0.76);
      actor.add(thigh);
      const foot = new THREE.Mesh(new THREE.BoxGeometry(0.56, 0.12, 0.3), this.materials.frogBelly);
      foot.position.set(0.2, 0.27, z * 1.2);
      actor.add(foot);
    });
    const mouth = new THREE.Mesh(
      new THREE.TubeGeometry(new THREE.CatmullRomCurve3([
        new THREE.Vector3(-1.3, 1.16, -0.34), new THREE.Vector3(-1.34, 1.09, 0), new THREE.Vector3(-1.3, 1.16, 0.34),
      ]), 10, 0.024, 5, false),
      this.materials.frogPupil,
    );
    mouth.name = 'frogMouth';
    actor.add(mouth);
    const mouthCavity = new THREE.Mesh(new THREE.SphereGeometry(0.3, 10, 7), this.materials.frogPupil);
    mouthCavity.name = 'frogMouthCavity';
    mouthCavity.position.set(-1.28, 1.08, 0);
    mouthCavity.scale.set(0.32, 0.16, 0.88);
    mouthCavity.userData.baseScaleY = mouthCavity.scale.y;
    actor.add(mouthCavity);
    const core = new THREE.Mesh(new THREE.IcosahedronGeometry(0.2, 1), this.materials.soul);
    core.name = 'baseNexusCore';
    core.position.set(-0.45, 2.15, 0);
    core.scale.set(0.68, 1.2, 0.68);
    actor.add(core);
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
    const sunBorn = kind === 'wisp' || kind === 'skyWarder';
    const body = new THREE.Mesh(
      kind === 'runner' ? new THREE.CapsuleGeometry(0.34, 0.74, 5, 8)
        : sunBorn ? new THREE.OctahedronGeometry(kind === 'skyWarder' ? 0.78 : 0.58, 1)
          : new THREE.DodecahedronGeometry(kind === 'swarm' ? 0.48 : 0.72, 0),
      bodyMaterial,
    );
    body.name = 'body';
    body.position.y = kind === 'swarm' ? 0.5 : 0.78;
    if (kind === 'runner') body.rotation.z = Math.PI / 2;
    body.scale.set(scale * 1.08, scale, scale * 0.9);
    root.add(body);
    const face = new THREE.Mesh(new THREE.OctahedronGeometry(kind === 'boss' ? 0.28 : 0.18, 0), this.materials.danger);
    face.position.set(0.55 * scale, 0.82 * scale, 0);
    root.add(face);
    const hornCount = kind === 'swarm' ? 2 : sunBorn ? 6 : kind === 'boss' ? 8 : 4;
    const horns = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(0.08 * scale, 0.42 * scale, 5), hornCount, (horn, index) => {
      const angle = index / hornCount * Math.PI * 2;
      horn.position.set(Math.cos(angle) * 0.56 * scale, 0.88 * scale, Math.sin(angle) * 0.56 * scale);
      horn.rotation.z = Math.PI / 2;
      horn.rotation.y = -angle;
    }), kind === 'armored' || kind === 'bulwark' || kind === 'skyWarder' ? this.materials.brass : this.materials.dryWood);
    root.add(horns);
    if (kind === 'armored' || kind === 'bulwark') this.addArmor(root, scale);
    if (kind === 'swarm') {
      const legs = new THREE.Mesh(mergeRepeatedGeometry(new THREE.CapsuleGeometry(0.035, 0.32, 3, 5), 6, (leg, index) => {
        const side = index < 3 ? -1 : 1;
        leg.position.set((index % 3 - 1) * 0.26, 0.25, side * 0.42);
        leg.rotation.x = side * Math.PI / 3;
      }), this.materials.dryWood);
      root.add(legs);
    }
    if (kind === 'runner') {
      const tail = new THREE.Mesh(new THREE.ConeGeometry(0.18, 0.9, 5), this.materials.dryWood);
      tail.position.set(-0.75, 0.55, 0);
      tail.rotation.z = Math.PI / 2;
      root.add(tail);
    }
    if (sunBorn) {
      const sunRing = new THREE.Mesh(new THREE.TorusGeometry((kind === 'skyWarder' ? 0.92 : 0.7) * scale, 0.07, 5, 24), this.materials.brass);
      sunRing.position.y = 0.84 * scale;
      sunRing.rotation.y = Math.PI / 2;
      root.add(sunRing);
    }
    if (kind === 'warded' || kind === 'bulwark' || kind === 'skyWarder' || kind === 'boss') {
      const ward = new THREE.Mesh(new THREE.SphereGeometry((kind === 'boss' ? 1.1 : 0.86), 14, 10), this.materials.ward.clone());
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
    const core = new THREE.Sprite(this.materials.projectileGlow(elements, reactionColor, 1));
    core.name = 'projectileCore';
    core.scale.setScalar(payload.reaction ? 0.7 : payload.baseElement ? 0.58 : 0.5);
    root.add(core);
    const halo = new THREE.Sprite(this.materials.projectileGlow(elements, reactionColor, payload.reaction ? 0.46 : 0.32));
    halo.name = 'projectileHalo';
    halo.scale.setScalar(payload.reaction ? 1.34 : payload.baseElement ? 1.02 : 0.86);
    root.add(halo);
    root.userData.presentation = 'billboard-glow';
    return root;
  }

  createDroughtRock(size: number, variant: number): THREE.Group {
    const root = new THREE.Group();
    root.name = 'drought-rock';
    const main = new THREE.Mesh(new THREE.DodecahedronGeometry(size, 0), variant % 2 === 0 ? this.materials.stone : this.materials.stoneLight);
    main.position.y = size * 0.62;
    main.scale.set(1.15, 0.78, 0.9);
    main.rotation.set(variant * 0.31, variant * 0.67, variant * 0.12);
    root.add(main);
    if (variant % 3 === 0) {
      const chip = new THREE.Mesh(new THREE.DodecahedronGeometry(size * 0.42, 0), this.materials.stoneLight);
      chip.position.set(size * 0.78, size * 0.24, size * 0.24);
      root.add(chip);
    }
    shadows(root);
    return root;
  }

  createDeadTree(size: number, variant: number): THREE.Group {
    const root = new THREE.Group();
    root.name = 'dead-tree';
    const trunk = new THREE.Mesh(new THREE.CylinderGeometry(size * 0.1, size * 0.16, size * 1.45, 6), this.materials.dryWood);
    trunk.position.y = size * 0.72;
    trunk.rotation.z = (variant % 2 === 0 ? 1 : -1) * 0.08;
    root.add(trunk);
    const branchData = [
      [-0.28, 1.05, -0.55], [0.3, 1.18, 0.62], [-0.18, 1.36, -0.78],
    ] as const;
    branchData.forEach(([x, y, tilt], index) => {
      const branch = new THREE.Mesh(new THREE.CylinderGeometry(size * 0.045, size * 0.075, size * (0.58 - index * 0.07), 5), this.materials.dryWood);
      branch.position.set(size * x, size * y, size * (index - 1) * 0.08);
      branch.rotation.z = tilt;
      branch.rotation.y = variant * 0.4 + index * 0.9;
      root.add(branch);
    });
    shadows(root);
    return root;
  }

  createWitheredGrassPatch(size: number, variant: number): THREE.Group {
    const root = new THREE.Group();
    root.name = 'withered-grass';
    const grass = new THREE.Mesh(mergeRepeatedGeometry(new THREE.ConeGeometry(size * 0.035, size * 0.58, 3), 7, (blade, index) => {
      const angle = index / 7 * Math.PI * 2;
      const radius = size * (0.12 + (index % 3) * 0.08);
      blade.position.set(Math.cos(angle) * radius, size * 0.28, Math.sin(angle) * radius);
      blade.rotation.z = (index % 2 === 0 ? 1 : -1) * (0.18 + variant * 0.015);
      blade.rotation.y = angle;
    }), this.materials.witheredGrass);
    root.add(grass);
    return root;
  }

  dispose(): void {
    this.baseGeometry.dispose();
    this.haloGeometry.dispose();
  }

  private decorateGenerator(root: THREE.Group): void {
    const furnace = new THREE.Mesh(new THREE.BoxGeometry(1.12, 0.92, 0.86), this.materials.terracotta);
    furnace.name = 'foundryBody';
    furnace.position.y = 1.02;
    root.add(furnace);
    const axle = new THREE.Mesh(new THREE.CylinderGeometry(0.2, 0.2, 1.34, 10), this.materials.brass);
    axle.name = 'foundryAxle';
    axle.position.set(0, 1.12, -0.5);
    axle.rotation.z = Math.PI / 2;
    root.add(axle);
    const wheel = new THREE.Mesh(new THREE.TorusGeometry(0.58, 0.13, 8, 18), this.materials.brass);
    wheel.name = 'foundryFlywheel';
    wheel.position.set(0, 1.22, -0.63);
    root.add(wheel);
    const hub = new THREE.Mesh(new THREE.CylinderGeometry(0.25, 0.25, 0.18, 10), this.materials.bone);
    hub.position.set(0, 1.22, -0.68);
    hub.rotation.x = Math.PI / 2;
    root.add(hub);
    [-0.36, 0.36].forEach((x) => {
      const stack = new THREE.Mesh(new THREE.CylinderGeometry(0.16, 0.22, 0.82, 8), this.materials.stone);
      stack.name = 'foundryChimney';
      stack.position.set(x, 1.82, 0.25);
      root.add(stack);
      const lip = new THREE.Mesh(new THREE.CylinderGeometry(0.23, 0.23, 0.12, 8), this.materials.brass);
      lip.position.set(x, 2.24, 0.25);
      root.add(lip);
    });
    const soul = new THREE.Mesh(new THREE.SphereGeometry(0.22, 10, 8), this.materials.bone);
    soul.name = 'chargeCore';
    soul.position.set(0, 1.1, -0.72);
    root.add(soul);
  }

  private decorateElement(root: THREE.Group, element: Element): void {
    if (element === 'fire') this.decorateFire(root);
    else if (element === 'ice') this.decorateIce(root);
    else if (element === 'wind') this.decorateWind(root);
    else this.decorateEarth(root);
  }

  private decorateFire(root: THREE.Group): void {
    root.userData.elementModel = 'wide-brazier';
    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(0.62, 0.78, 0.72, 10), this.materials.stoneLight);
    pedestal.name = 'firePedestal';
    pedestal.position.y = 0.86;
    root.add(pedestal);
    const bowl = new THREE.Mesh(new THREE.CylinderGeometry(0.9, 0.56, 0.42, 12, 1, true), this.materials.terracotta);
    bowl.name = 'fireBrazier';
    bowl.position.y = 1.34;
    root.add(bowl);
    const rim = new THREE.Mesh(new THREE.TorusGeometry(0.86, 0.1, 6, 36), this.materials.brass);
    rim.rotation.x = Math.PI / 2;
    rim.position.y = 1.49;
    root.add(rim);
    const flameGroup = new THREE.Group();
    flameGroup.name = 'spinner';
    flameGroup.position.y = 1.72;
    const flameParts = [
      { x: 0, y: 0.48, z: 0, scale: [0.72, 1.62, 0.72] },
      { x: -0.34, y: 0.18, z: 0.09, scale: [0.44, 0.96, 0.44] },
      { x: 0.34, y: 0.12, z: -0.11, scale: [0.4, 0.86, 0.4] },
    ] as const;
    flameParts.forEach((part, index) => {
      const flame = new THREE.Mesh(new THREE.ConeGeometry(0.42, 1.05, 6), this.materials.element('fire'));
      flame.name = index === 0 ? 'elementCore' : 'fireTongue';
      flame.position.set(part.x, part.y, part.z);
      flame.scale.set(part.scale[0], part.scale[1], part.scale[2]);
      flame.rotation.z = (index - 1) * 0.18;
      flameGroup.add(flame);
    });
    root.add(flameGroup);
  }

  private decorateIce(root: THREE.Group): void {
    root.userData.elementModel = 'asymmetric-crystal-crown';
    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(0.56, 0.74, 0.76, 6), this.materials.stoneLight);
    pedestal.name = 'icePedestal';
    pedestal.position.y = 0.9;
    root.add(pedestal);
    const prism = new THREE.Mesh(new THREE.ConeGeometry(0.5, 1.78, 6), this.materials.element('ice'));
    prism.name = 'elementCore';
    prism.position.set(-0.08, 1.92, 0.04);
    root.add(prism);
    const shardGeometry = mergeRepeatedGeometry(new THREE.ConeGeometry(0.2, 0.92, 5), 5, (shard, index) => {
      const angle = index / 5 * Math.PI * 2;
      shard.position.set(Math.cos(angle) * 0.62, 1.38 + (index % 3) * 0.16, Math.sin(angle) * 0.62);
      shard.rotation.z = (index % 2 === 0 ? 1 : -1) * (0.28 + index * 0.035);
      shard.rotation.y = -angle;
    });
    const shards = new THREE.Mesh(shardGeometry, this.materials.element('ice'));
    shards.name = 'iceShardCrown';
    root.add(shards);
    const halo = new THREE.Mesh(runeShape(8, 0.68, 0.52), this.materials.bone);
    halo.name = 'spinner';
    halo.rotation.x = -Math.PI / 2;
    halo.position.y = 1.18;
    root.add(halo);
  }

  private decorateWind(root: THREE.Group): void {
    root.userData.elementModel = 'broad-wind-rotor';
    const pedestal = new THREE.Mesh(new THREE.CylinderGeometry(0.28, 0.54, 1.48, 8), this.materials.stoneLight);
    pedestal.name = 'windPedestal';
    pedestal.position.y = 1.12;
    root.add(pedestal);
    const rotor = new THREE.Group();
    rotor.name = 'windSpinner';
    rotor.position.y = 2.02;
    rotor.rotation.x = -0.18;
    const vaneShape = new THREE.Shape();
    vaneShape.moveTo(0.08, 0.04);
    vaneShape.lineTo(1.12, 0.18);
    vaneShape.quadraticCurveTo(1.03, 0.74, 0.34, 0.82);
    vaneShape.lineTo(0.14, 0.28);
    vaneShape.closePath();
    const vaneGeometry = new THREE.ExtrudeGeometry(vaneShape, { depth: 0.08, bevelEnabled: true, bevelSize: 0.025, bevelThickness: 0.025, bevelSegments: 1 });
    for (let index = 0; index < 4; index += 1) {
      const vane = new THREE.Mesh(vaneGeometry, this.materials.element('wind'));
      vane.name = index === 0 ? 'windVane' : 'windVanePart';
      vane.rotation.z = index * Math.PI / 2;
      vane.position.z = -0.04;
      rotor.add(vane);
    }
    const hub = new THREE.Mesh(new THREE.IcosahedronGeometry(0.32, 1), this.materials.brass);
    hub.name = 'elementCore';
    hub.position.z = 0.08;
    rotor.add(hub);
    root.add(rotor);
    const forkGeometry = mergeRepeatedGeometry(new THREE.CapsuleGeometry(0.055, 0.54, 3, 5), 2, (fork, index) => {
      fork.position.set(0, 1.6, (index === 0 ? -1 : 1) * 0.5);
    });
    root.add(new THREE.Mesh(forkGeometry, this.materials.brass));
  }

  private decorateEarth(root: THREE.Group): void {
    root.userData.elementModel = 'squat-stepped-monolith';
    const lower = new THREE.Mesh(new THREE.BoxGeometry(1.28, 0.72, 1.12), this.materials.stoneLight);
    lower.name = 'earthMonolith';
    lower.position.y = 0.86;
    lower.rotation.y = 0.12;
    root.add(lower);
    const upper = new THREE.Mesh(new THREE.BoxGeometry(0.94, 0.72, 0.82), this.materials.stone);
    upper.position.set(-0.12, 1.5, 0.06);
    upper.rotation.y = -0.16;
    root.add(upper);
    const cap = new THREE.Mesh(new THREE.DodecahedronGeometry(0.43, 0), this.materials.element('earth'));
    cap.name = 'elementCore';
    cap.position.set(0.16, 2.02, -0.06);
    cap.scale.set(1.22, 0.68, 1.06);
    root.add(cap);
    [-0.72, 0.72].forEach((x, index) => {
      const buttress = new THREE.Mesh(new THREE.DodecahedronGeometry(index === 0 ? 0.34 : 0.29, 0), this.materials.element('earth'));
      buttress.name = 'earthButtress';
      buttress.position.set(x, 0.94 + index * 0.12, index === 0 ? -0.2 : 0.23);
      buttress.scale.set(1.05, 1.34, 0.92);
      root.add(buttress);
    });
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
    const bell = new THREE.Mesh(new THREE.ConeGeometry(0.72, 1.35, 8, 1, true), this.materials.terracotta);
    bell.position.y = 1.23;
    root.add(bell);
    const heart = new THREE.Mesh(new THREE.IcosahedronGeometry(0.34, 1), this.materials.projectile([], 0xff9f65));
    heart.name = 'pulseCore';
    heart.position.y = 1.56;
    root.add(heart);
    for (let index = 0; index < 6; index += 1) {
      const rib = new THREE.Mesh(new THREE.ConeGeometry(0.08, 0.72, 5), this.materials.brass);
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
