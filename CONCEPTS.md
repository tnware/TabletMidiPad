# TabletMidiPad: Vision, Use Cases, and Concepts

This document serves as a reference for the vision behind `TabletMidiPad`, exploring its potential as a creative tool, its primary use cases, and missing concepts that could further enhance its utility.

---

## 1. Vision and Purpose

The core vision of `TabletMidiPad` is to transform **ubiquitous, often-underutilized hardware** (Wacom tablets, pens, and styluses) into a **high-precision, expressive MIDI controller**.

While dedicated MIDI hardware can be expensive, many artists and designers already own high-quality digitizing tablets that offer:
- **Absolute Positioning**: Unlike a mouse or most knobs, a pen on a tablet has a direct relationship between physical space and parameter value.
- **High Resolution**: Wacom digitizers often report thousands of levels of pressure and high-resolution XY coordinates.
- **Tactile Feedback**: The physical resistance of a pen on a tablet surface provides a unique "feel" for musical performance.

`TabletMidiPad` bridges the gap between these high-fidelity input devices and the world of digital audio workstations (DAWs).

---

## 2. Use Cases

### A. Repurposing Legacy Hardware
Old tablet hardware (e.g., Intuos 3, Bamboo, older Graphire models) that might be "outdated" for modern professional illustration often still has perfectly functional digitizers. `TabletMidiPad` gives these devices a second life as dedicated studio controllers.

### B. Controlled Precise Adjustments
For mixing and sound design, the absolute positioning of a tablet allows for:
- **Instant Jumps**: Move the pen directly to a location to set a value, rather than "dragging" from the current position.
- **Visual Mapping**: If the tablet surface matches the screen layout of a plugin, it becomes a literal 1:1 control surface.
- **Multi-Parameter Gating**: Using zones to control different filters or effects simultaneously without looking at the screen.

### C. Wild Creative Expression
In a live performance or experimental sound design context, the tablet becomes a "performative instrument":
- **Fluid Gestures**: Sweeping the pen across the surface to create complex automation curves that are difficult to draw with a mouse or record with a knob.
- **Pressure-Driven Dynamics**: Using pen pressure to control volume, filter resonance, or distortion intensity in real-time.
- **Pen "Percussion"**: Tapping the pen on different zones to trigger samples with varying velocity based on hit pressure.

---

## 3. Core Concepts (Existing)

- **Zoning**: Partitioning the physical surface into logical rectangles, each sending separate MIDI data.
- **Three-Axis Control**: Every point on the tablet provides three independent variables (X, Y, and Pressure).
- **ExpressKey Overrides**: Utilizing hardware buttons for momentary or toggle actions (e.g., locking an axis or sending a specific CC/Note).

---

## 4. Missing Concepts and Future Directions

The following concepts represent potential areas for growth, expanding `TabletMidiPad` from a CC-mapper into a comprehensive performance environment.

### A. Paging and Layout Layers
Currently, the tablet is a single static layout.
- **Concept**: Use ExpressKeys to cycle through different "Pages" of zones.
- **Use Case**: Page 1 could be a mixer layout; Page 2 could be an XY pad for a synth; Page 3 could be a set of drum pads.
- **Implementation**: A "Global Page Index" in `AppConfig` that filters which zones are active.

### B. Note Grids and Musical Expression Pads
Instead of just sending CCs (Continuous Controllers), zones could behave as musical interfaces.
- **Note Grid**: Partitioning a zone into a grid (e.g., 4x4 or 8x8) where each sub-cell triggers a specific MIDI note.
- **Velocity from Pressure**: Using the initial strike pressure to determine MIDI velocity.
- **Aftertouch**: Using sustained pressure while the pen is down to send MIDI Aftertouch (Channel or Polyphonic).
- **Pitch Bend / Vibrato**: Using small X/Y movements after a note is triggered to control pitch or modulation.

### C. The "Expression Zone" (MPE-Style)
A more advanced version of the note grid, where each zone represents a single "center-tuned" musical note but allows for continuous expressive deviation.
- **Center-Tuned Note**: The dead center of the zone emits a 100% "on key" note.
- **Divergence (X/Y)**: Moving away from the center of the zone applies continuous pitch bend, timbre shifts, or other modifiers. For example, the X-axis could control fine-tuning, allowing the performer to play subtly "out of key" for a more human or experimental sound.
- **Pressure Integration**: Pressure can simultaneously control note velocity at the start and aftertouch/modulation throughout the note's duration.
- **Expressive Latching**: Incorporating latch controls or synth-style ADSR modifiers that respond to how the pen is held or released within the zone.

### D. Hybrid Performance Integration
A zone shouldn't be limited to *either* notes *or* CCs.
- **Concept**: Map a zone to emit both a musical note (based on pen down/hit) and simultaneous CC data mapped to specific parameters within the same plugin or instrument.
- **Use Case**: Playing a note in a synth while simultaneously using the X and Y movement to modulate the synth's internal filter cutoff and resonance. This allows for deeply integrated performance where the "playing" and "tweaking" happen in a single fluid gesture.
- **Meaningful Mapping**: Providing a way to link tablet axes to DAW/Plugin parameters in a way that feels musically coherent (e.g., Y-axis for brightness, X-axis for stereo width).

### E. MPE (MIDI Polyphonic Expression)
Modern Wacom pens often support **Tilt** (X/Y) and sometimes **Rotation**.
- **Concept**: Map pen tilt to MPE dimensions (e.g., Slide or Timbre).
- **Use Case**: Tilting the pen forward to open a filter while moving it across the pad to change pitch.

### F. Advanced Gesture Support
- **Flicks and Swipes**: Recognizing quick movements to trigger specific events (e.g., "Flick left" to undo).
- **Dwell Actions**: Triggering a MIDI event if the pen stays still in a zone for a certain duration.

### G. Visual Feedback (HUD & Overlays)
- **Dynamic Labels**: Showing the name of the parameter being controlled near the cursor dot.
- **Ghosting**: Visualizing the "path" of the last few pen movements to help the user understand the automation being generated.

---

## 5. Summary: The Ultimate Creative Tool
By combining low-cost, high-precision hardware with flexible software mapping, `TabletMidiPad` allows for a level of creative expression usually reserved for specialized (and expensive) controllers like the Sensel Morph or the Haken Continuum. Whether used for precise mixing or wild sonic exploration, the potential for tablet-based MIDI control is vast and still largely untapped.
