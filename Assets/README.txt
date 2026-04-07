This folder is for application assets (icons, images).

- Add an application icon named `app.ico` to enable the app to display a proper icon.
  Recommended sizes inside the .ico: 16x16, 32x32, 48x48, 256x256.

- Add `icon.png` (e.g., 256x256) if you need to reference a raster image in XAML.

- After adding assets, commit them and the project will automatically pick up `Assets\app.ico` as the ApplicationIcon (see .csproj).
