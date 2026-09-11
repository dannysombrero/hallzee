# Hallzee 3D models

The repository's canonical 3D-model folder is:

[Browse Hallzee 3D models](../models/)

Use it for enclosure, mounting, and other fabrication files. 3D models and fabrication assets in `models/` are licensed under [Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)](license.md).

Production `.3mf` models for 2.8″ terminals and historical 1.8″ profiles are available in the repository.

## 2.8″ Terminal 3D Renders

### Assembled Terminal
Fully assembled Hallzee 2.8″ terminal enclosure with 3×4 membrane keypad and ILI9341 display:

<p align="center">
  <img src="images/Hallzee_Terminal_3D_Render_2.png" alt="Hallzee 2.8-inch terminal assembled 3D render with display and keypad" width="480" />
</p>

### Enclosure Shell
Modular front cover and main body printed in PLA:

<p align="center">
  <img src="images/Hallzee_Terminal_3D_Render_1.png" alt="Hallzee 2.8-inch terminal enclosure shell 3D render" width="480" />
</p>

### CAD Assembly and Port Geometry
CAD model view displaying the enclosure contours, internal component fit, and side micro-USB cable cutout:

<p align="center">
  <img src="images/Hallzee_Terminal_3D_Render.png" alt="Hallzee 2.8-inch terminal CAD assembly wireframe render" width="560" />
</p>

## Fabrication Files

The current 2.8″ terminal enclosure consists of:
- **Main Case** (`Hallzee_Terminal_2.8''_Main_Case_v1.3mf`): Main enclosure body housing the ESP32 DevKit V1 and display mounting posts.
- **Back Cover** (`Hallzee_Terminal_2.8''_Back_v1.3mf`): Rear enclosure plate with mounting holes.
- **LCD and Keypad Cover** (`Hallzee_Terminal_2.8''_LCD_and_Keypad_Cover_v1.3mf`): Front bezel securing the 2.8″ TFT and 3×4 membrane keypad.
- **Keypad Support** (`Hallzee_Terminal_2.8''_Keypad_BackSupport_V1.3mf`): Internal bracket providing rigid backing behind the membrane keypad.
- **ESP32 Stabilizer** (`Hallzee_Terminal_2.8''_ESP32_Stabilizer.3mf`): Internal stabilizer clip locking the ESP32 board in position.
- **Complete CAD Model** (`Hallzee_Case_Whole_v1.step`): Full assembly STEP file for customization in CAD software (Fusion 360, FreeCAD, Onshape, etc.).

Historical 1.8″ terminal profiles are archived in [`models/Historical/`](../models/Historical/).

For the supported components, no-solder wiring, and assembly instructions, see
[Build your own terminal](build-your-own-terminal.md).
