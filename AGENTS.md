# Directrices de producto y experiencia de usuario

GoIsland es una aplicación orientada a usuarios finales. Toda interfaz visible debe sentirse como un producto terminado, no como una herramienta técnica ni una demostración del sistema.

## Redacción de interfaz

- Usar textos breves, naturales y centrados en la acción que la persona puede realizar.
- Mostrar solamente la información necesaria para entender la pantalla o tomar una decisión.
- No explicar detalles internos de implementación, configuración o arquitectura.
- No mencionar en la interfaz conceptos como backend, API, tokens, proveedores internos, base de datos, códigos de estado o nombres técnicos de roles.
- No explicar por qué una función no existe cuando basta con ocultarla. Por ejemplo, una cuenta que accede con Google puede mostrar “Acceso con Google”, pero no debe mostrar “Cambiar contraseña” ni explicar que no tiene contraseña.
- Evitar textos preventivos sobre límites técnicos. Informar un límite solamente cuando el usuario lo alcance o intente superarlo.
- Traducir los errores técnicos a mensajes claros que indiquen qué ocurrió y qué puede hacer la persona.
- Evitar instrucciones largas, justificaciones del diseño y textos que describan el funcionamiento interno.

## Comportamiento de la interfaz

- Ocultar acciones que no sean aplicables al estado o tipo de cuenta actual.
- Usar divulgación progresiva: mostrar detalles, restricciones y opciones avanzadas únicamente cuando sean relevantes.
- Priorizar acciones directas, estados comprensibles y retroalimentación inmediata.
- Mantener las pantallas de administración y anfitriones eficientes, pero usar vocabulario del negocio en lugar de terminología técnica.
- Los mensajes de validación deben aparecer cerca del campo o acción correspondiente y solamente cuando sean necesarios.

## Revisión antes de entregar cambios visuales

Antes de considerar terminada una pantalla, comprobar:

1. ¿El texto ayuda al usuario a actuar?
2. ¿Hay explicaciones técnicas o información que el usuario no necesita?
3. ¿Se puede ocultar una acción no disponible en vez de justificar su ausencia?
4. ¿Los límites se comunican solo cuando se alcanzan?
5. ¿La pantalla se entiende sin conocer cómo está construido el sistema?

Estas reglas aplican a todo texto y comportamiento visible en frontend, correos transaccionales y notificaciones. No limitan la documentación técnica, los logs ni los mensajes exclusivos del entorno de desarrollo.
