# Aviation icon sources

The vector silhouettes in `src/ATCsim.UI/Resources/AviationIcons.xaml` are adapted
from the following CC0 1.0 sources. Only the SVG path geometry is included; web
scripts, filters, shadows, colors and strokes are omitted. WPF sizes and rotates
the silhouettes for the radar.

- Passenger jet: Mapbox Maki `icons/airport.svg`:
  https://github.com/mapbox/maki/blob/main/icons/airport.svg
- Regional jet, cargo, business jet and propeller: rikgale/VRSCustomMarkers
  `MyMarkers1.html`, markers `TypeE145`, `TypeIL76`, `TypeGLEX`, `TypeSR20`:
  https://github.com/rikgale/VRSCustomMarkers/blob/main/MyMarkers1.html
  The regional marker represents the Embraer jet family; the Cirrus marker is
  shared by SR20 and SR22 in the source collection.
- Airport terminal/tower: Rapid Editor Temaki `icons/airport.svg`:
  https://github.com/rapideditor/temaki/blob/main/icons/airport.svg

Original license texts are retained in `docs/licenses/`.

The ATCsim logo in `src/ATCsim.UI/Assets/logo.png` is an unchanged copy of this
project's existing `docs/logo.png` from https://github.com/dyxell1n/ATCsim.
