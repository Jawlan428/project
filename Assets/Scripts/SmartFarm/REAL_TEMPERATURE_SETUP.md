# Real Temperature Setup (OpenWeather)

`RealTemperatureService` is added automatically to `FarmSimulationHub` during Farm Setup.

## 1) Get API key

1. Create free account on OpenWeather.
2. Generate API key.

## 2) Configure in Unity

1. Select `FarmSimulationHub` in scene.
2. In `RealTemperatureService`:
   - `Enable Real Temperature` = ON
   - `Api Key` = your key
   - Choose location:
     - `CityName` + `City` (+ optional `CountryCode`)
     - or `Coordinates` (`Latitude`, `Longitude`)
   - `Refresh Interval Seconds` = e.g. 300

## 3) Play

- The service fetches current temperature in Celsius and calls:
  - `FarmSimulationManager.SetGlobalTemperature(...)`
- Dashboard/tablet temperature updates automatically.

## Notes

- If API request fails, optional fallback temperature is used.
- Keep key private; do not commit real keys to public repos.
