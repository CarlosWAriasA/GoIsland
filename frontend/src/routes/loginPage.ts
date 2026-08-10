// La pantalla de inicio de sesión se descarga aparte, como el resto de páginas, pero es la única
// a la que se llega justo cuando la sesión acaba de cerrarse: si esa descarga falla en ese preciso
// momento no queda ninguna pantalla que mostrar. Se declara aquí para poder traerla por adelantado
// y para que quien cierra sesión pida exactamente el mismo fragmento que la ruta.
export const loadLoginPage = () => import('../pages/Login');

export default loadLoginPage;
