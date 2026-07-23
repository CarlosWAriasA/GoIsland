const tiles = [
  'playa',
  'buceo',
  'naturaleza',
  'gastronomia',
  'ciudad',
  'mar',
] as const;

// Mosaico decorativo de fotografías de ambiente de República Dominicana
// (licencia libre). No representa experiencias reales del backend.
export const AuthMosaic = () => (
  <div className="auth-mosaic" aria-hidden="true">
    {tiles.map((tile) => (
      <span className={`auth-mosaic__tile auth-mosaic__tile--${tile}`} key={tile} />
    ))}
  </div>
);

export default AuthMosaic;
