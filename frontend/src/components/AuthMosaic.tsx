const tiles = [
  'playa',
  'buceo',
  'naturaleza',
  'gastronomia',
  'ciudad',
  'mar',
] as const;

export const AuthMosaic = () => (
  <div className="auth-mosaic" aria-hidden="true">
    {tiles.map((tile) => (
      <span className={`auth-mosaic__tile auth-mosaic__tile--${tile}`} key={tile} />
    ))}
  </div>
);

export default AuthMosaic;
