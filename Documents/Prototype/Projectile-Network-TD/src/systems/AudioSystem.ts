type UiSound = 'select' | 'confirm' | 'error' | 'upgrade' | 'sell';

export class AudioSystem {
  private context: AudioContext | null = null;
  private master: GainNode | null = null;
  private muted = false;
  private paused = false;
  private unlocked = false;
  private noiseState = 0x20260816;
  private readonly active = new Set<AudioScheduledSourceNode>();

  constructor() {
    const unlock = () => { void this.unlock(); };
    window.addEventListener('pointerdown', unlock, { once: true, capture: true });
    window.addEventListener('keydown', unlock, { once: true, capture: true });
  }

  async unlock(): Promise<void> {
    if (!this.context) {
      const AudioContextClass = window.AudioContext
        || (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
      if (!AudioContextClass) return;
      this.context = new AudioContextClass();
      this.master = this.context.createGain();
      this.master.gain.value = this.muted ? 0 : 0.68;
      this.master.connect(this.context.destination);
    }
    if (this.context.state !== 'running') await this.context.resume();
    this.unlocked = true;
  }

  isMuted(): boolean {
    return this.muted;
  }

  toggleMute(): boolean {
    this.muted = !this.muted;
    if (this.context && this.master) {
      this.master.gain.setTargetAtTime(this.muted ? 0 : 0.68, this.context.currentTime, 0.015);
    }
    return this.muted;
  }

  setPaused(paused: boolean): void {
    this.paused = paused;
  }

  ui(kind: UiSound): void {
    const recipes: Record<UiSound, readonly [number, number, number, OscillatorType]> = {
      select: [440, 560, 0.07, 'sine'],
      confirm: [520, 820, 0.12, 'triangle'],
      error: [170, 105, 0.16, 'square'],
      upgrade: [390, 980, 0.2, 'triangle'],
      sell: [620, 290, 0.15, 'sine'],
    };
    const [from, to, duration, type] = recipes[kind];
    this.tone(from, to, duration, type, kind === 'error' ? 0.085 : 0.055);
  }

  build(): void {
    this.tone(115, 210, 0.11, 'square', 0.06);
    this.noise(0.055, 0.055, 900);
  }

  wave(): void {
    this.chord([196, 294, 392], 0.34, 'triangle', 0.045);
  }

  shot(elementCount: number): void {
    const pitch = 280 + elementCount * 85;
    this.tone(pitch, pitch * 1.7, 0.075, 'sawtooth', 0.025);
  }

  infuse(elementCount: number): void {
    const pitch = 420 + Math.min(4, elementCount) * 95;
    this.tone(pitch, pitch * 1.42, 0.13, 'triangle', 0.045);
  }

  hit(): void {
    this.noise(0.035, 0.028, 1350);
  }

  reaction(): void {
    this.chord([330, 495, 742], 0.21, 'sine', 0.04);
    this.noise(0.12, 0.035, 2400);
  }

  leak(): void {
    this.tone(150, 58, 0.34, 'sawtooth', 0.08);
  }

  destroy(): void {
    this.tone(210, 75, 0.18, 'square', 0.04);
    this.noise(0.16, 0.065, 720);
  }

  special(): void {
    this.tone(180, 1180, 0.42, 'sawtooth', 0.06);
    window.setTimeout(() => this.noise(0.24, 0.09, 3200), 170);
  }

  waveClear(): void {
    this.chord([392, 494, 659], 0.48, 'triangle', 0.048);
  }

  win(): void {
    this.chord([262, 330, 392, 523], 0.85, 'triangle', 0.055);
  }

  lose(): void {
    this.chord([247, 196, 147], 0.7, 'sawtooth', 0.04);
  }

  reset(): void {
    for (const source of this.active) {
      try { source.stop(); } catch { /* Already stopped. */ }
    }
    this.active.clear();
  }

  dispose(): void {
    this.reset();
    void this.context?.close();
    this.context = null;
    this.master = null;
    this.unlocked = false;
  }

  private tone(from: number, to: number, duration: number, type: OscillatorType, volume: number): void {
    if (!this.canPlay()) return;
    const context = this.context;
    const master = this.master;
    if (!context || !master) return;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    const now = context.currentTime;
    oscillator.type = type;
    oscillator.frequency.setValueAtTime(Math.max(1, from), now);
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, to), now + duration);
    gain.gain.setValueAtTime(0.0001, now);
    gain.gain.exponentialRampToValueAtTime(volume, now + 0.008);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    oscillator.connect(gain).connect(master);
    this.track(oscillator);
    oscillator.start(now);
    oscillator.stop(now + duration + 0.015);
  }

  private chord(frequencies: readonly number[], duration: number, type: OscillatorType, volume: number): void {
    for (const [index, frequency] of frequencies.entries()) {
      window.setTimeout(() => this.tone(frequency, frequency * 1.02, duration, type, volume), index * 38);
    }
  }

  private noise(duration: number, volume: number, cutoff: number): void {
    if (!this.canPlay()) return;
    const context = this.context;
    const master = this.master;
    if (!context || !master) return;
    const sampleCount = Math.max(1, Math.floor(context.sampleRate * duration));
    const buffer = context.createBuffer(1, sampleCount, context.sampleRate);
    const data = buffer.getChannelData(0);
    for (let index = 0; index < sampleCount; index += 1) data[index] = this.nextNoiseSample();
    const source = context.createBufferSource();
    const filter = context.createBiquadFilter();
    const gain = context.createGain();
    const now = context.currentTime;
    source.buffer = buffer;
    filter.type = 'lowpass';
    filter.frequency.value = cutoff;
    gain.gain.setValueAtTime(volume, now);
    gain.gain.exponentialRampToValueAtTime(0.0001, now + duration);
    source.connect(filter).connect(gain).connect(master);
    this.track(source);
    source.start(now);
    source.stop(now + duration + 0.015);
  }

  private canPlay(): boolean {
    return Boolean(this.unlocked && !this.muted && !this.paused && this.context?.state === 'running');
  }

  private track(source: AudioScheduledSourceNode): void {
    this.active.add(source);
    source.addEventListener('ended', () => this.active.delete(source), { once: true });
  }

  private nextNoiseSample(): number {
    this.noiseState = (Math.imul(this.noiseState, 1664525) + 1013904223) >>> 0;
    return this.noiseState / 4294967296 * 2 - 1;
  }
}
