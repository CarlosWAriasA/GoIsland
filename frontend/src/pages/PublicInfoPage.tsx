import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ArrowRight, FileText, ShieldCheck, TicketX, Mail } from 'lucide-react';
import Alert from '../components/Alert';
import { usePageMetadata } from '../hooks/usePageMetadata';

type PublicPageKey = 'contact' | 'privacy' | 'terms' | 'cancellations';

interface PublicInfoPageProps {
  page: PublicPageKey;
}

interface InfoSection {
  id: string;
  title: string;
  paragraphs?: string[];
  items?: string[];
}

interface InfoPage {
  eyebrow: string;
  title: string;
  description: string;
  updatedAt: string;
  notice: string;
  sections: InfoSection[];
  related: PublicPageKey[];
}

const pages = {
  contact: {
    eyebrow: 'Contacto',
    title: 'Estamos para orientarte',
    description: 'Canales de comunicación con el equipo responsable de GoIsland y qué esperar de cada uno.',
    updatedAt: '10 de agosto de 2026',
    notice: 'GoIsland es un proyecto académico. No compartas contraseñas, números de tarjeta ni documentos de identidad por ningún canal.',
    sections: [
      {
        id: 'canales',
        title: '1. Cómo comunicarte con nosotros',
        paragraphs: [
          'La vía habitual de contacto es el correo con el que registraste tu cuenta: todas las confirmaciones, recordatorios y avisos de cambios de reserva salen desde ahí, y puedes responder a cualquiera de esos mensajes.',
          'Si estás en una presentación o demostración del proyecto, plantea tus preguntas directamente al equipo expositor. Es la forma más rápida de resolver dudas sobre el funcionamiento.',
        ],
      },
      {
        id: 'reservas',
        title: '2. Dudas sobre una reserva concreta',
        paragraphs: [
          'La mayoría de las gestiones no necesitan que nos escribas. Desde “Mis reservas” puedes ver el estado, consultar el detalle, pedir un cambio de fecha o solicitar la cancelación al anfitrión.',
          'Cuando escribas por una reserva, incluye su número y la fecha de la actividad. Sin ese dato no podemos identificarla.',
        ],
      },
      {
        id: 'anfitriones',
        title: '3. Si quieres publicar experiencias',
        paragraphs: [
          'Cualquier persona con cuenta puede solicitar el perfil de anfitrión desde “Quiero ser anfitrión”. La solicitud queda en revisión y recibirás una notificación cuando se apruebe o se rechace.',
          'Antes de escribir, revisa que tu descripción, tu ubicación y tus horarios estén completos: la mayoría de los rechazos se deben a información incompleta, no al tipo de actividad.',
        ],
      },
      {
        id: 'reportes',
        title: '4. Reportar contenido o comportamiento',
        paragraphs: [
          'Si encuentras una publicación engañosa, una reseña ofensiva o un perfil sospechoso, indícanos el enlace de la página y qué te llamó la atención. El equipo de moderación puede ocultar reseñas, despublicar experiencias y suspender perfiles de anfitrión.',
        ],
      },
      {
        id: 'privacidad-contacto',
        title: '5. Ejercicio de derechos sobre tus datos',
        paragraphs: [
          'Las solicitudes de acceso, rectificación o eliminación de datos personales se atienden por correo, escribiendo desde la dirección registrada en la cuenta. El detalle del procedimiento está en la Política de privacidad.',
        ],
      },
      {
        id: 'tiempos',
        title: '6. Qué no gestionamos por estos canales',
        items: [
          'Cambios de contraseña: se hacen desde tu perfil o con el enlace de recuperación.',
          'Datos de pago: los procesa Stripe; GoIsland no recibe ni almacena números de tarjeta.',
          'Reembolsos fuera de la plataforma: cualquier devolución se registra sobre el pago original.',
          'Acuerdos privados con un anfitrión: si la reserva no pasó por GoIsland, no podemos intervenir.',
        ],
      },
    ],
    related: ['privacy', 'terms', 'cancellations'],
  },
  privacy: {
    eyebrow: 'Privacidad',
    title: 'Política de privacidad',
    description: 'Qué datos personales tratamos en GoIsland, con qué finalidad, con quién se comparten y cómo ejercer tus derechos.',
    updatedAt: '10 de agosto de 2026',
    notice: 'GoIsland es un prototipo universitario. Usa datos ficticios en tus pruebas y no ingreses información personal sensible.',
    sections: [
      {
        id: 'responsable',
        title: '1. Responsable del tratamiento',
        paragraphs: [
          'El responsable del tratamiento de los datos es el equipo académico que desarrolla y opera GoIsland en República Dominicana. El tratamiento sigue los principios de la Ley núm. 172-13 sobre protección de datos personales: consentimiento, finalidad determinada, proporcionalidad y calidad de la información.',
          'Esta política aplica al sitio web de GoIsland y a los correos y notificaciones que enviamos desde él.',
        ],
      },
      {
        id: 'datos',
        title: '2. Datos que tratamos',
        paragraphs: [
          'Solo recogemos datos que necesitamos para que la plataforma funcione. Puedes usar GoIsland sin cuenta para explorar el catálogo y el mapa; a partir de la reserva sí hace falta registrarse.',
        ],
        items: [
          'Datos de cuenta: nombre, correo electrónico y una versión cifrada de tu contraseña (hash PBKDF2). Si entras con Google, recibimos tu nombre, correo y foto de perfil de esa cuenta.',
          'Datos de perfil: teléfono, foto y, en el caso de anfitriones, biografía, idiomas y datos de la actividad que publiques.',
          'Datos de reserva: experiencia, fecha, cantidad de personas, estado de la reserva y notas que escribas al anfitrión.',
          'Datos de pago: importe, moneda, estado del cobro e identificadores que genera Stripe. Los números de tarjeta se introducen en un formulario de Stripe y nunca llegan a nuestros servidores.',
          'Contenido que publicas: reseñas, calificaciones, imágenes de experiencias y respuestas de anfitrión.',
          'Datos técnicos: dirección IP, tipo de navegador y registros de error necesarios para mantener el servicio y detectar abusos.',
          'Ubicación aproximada: solo si pulsas “Cerca de mí” en el mapa y autorizas el permiso del navegador. No la guardamos en la base de datos.',
          'Suscripción a notificaciones push: el identificador que emite tu navegador, únicamente si aceptas activarlas.',
        ],
      },
      {
        id: 'finalidades',
        title: '3. Para qué usamos tus datos',
        items: [
          'Crear y mantener tu cuenta, y validar tu identidad al iniciar sesión.',
          'Gestionar reservas: confirmarlas, avisar de cambios, recordarte la visita y liberar cupos cuando una reserva pendiente vence.',
          'Procesar cobros y devoluciones a través de Stripe.',
          'Poner en contacto a la persona viajera con el anfitrión de su reserva, con los datos mínimos necesarios para prestar la actividad.',
          'Moderar contenido, prevenir fraudes y aplicar los Términos de uso.',
          'Enviar avisos operativos por correo y, si las activaste, notificaciones push.',
        ],
      },
      {
        id: 'legitimacion',
        title: '4. Base que legitima el tratamiento',
        paragraphs: [
          'Tratamos los datos de cuenta, reserva y pago porque son imprescindibles para ejecutar el servicio que solicitas. Los datos técnicos y de moderación responden al interés legítimo de mantener la plataforma segura. La ubicación y las notificaciones push dependen exclusivamente de tu consentimiento, que puedes retirar en cualquier momento desde los permisos del navegador.',
        ],
      },
      {
        id: 'terceros',
        title: '5. Con quién compartimos datos',
        paragraphs: [
          'No vendemos datos personales ni los cedemos con fines publicitarios. Compartimos únicamente lo necesario con los proveedores que hacen funcionar el servicio:',
        ],
        items: [
          'Anfitrión de tu reserva: nombre, cantidad de personas y las notas que hayas escrito.',
          'Stripe: procesamiento de pagos y devoluciones.',
          'Google Maps: consulta de mapas y geocodificación de las ubicaciones publicadas.',
          'Cloudinary: almacenamiento y entrega de las imágenes de experiencias y perfiles.',
          'Proveedor de correo saliente: envío de confirmaciones y recuperación de contraseña.',
          'Infraestructura de alojamiento y base de datos que sostiene la aplicación.',
          'Autoridades competentes, cuando exista un requerimiento legal válido.',
        ],
      },
      {
        id: 'cookies',
        title: '6. Cookies y almacenamiento local',
        paragraphs: [
          'GoIsland no usa cookies publicitarias ni de seguimiento de terceros. Utilizamos el almacenamiento del navegador para dos cosas: mantener tu sesión iniciada y recordar los últimos filtros de búsqueda que aplicaste en el catálogo.',
          'Si borras los datos del sitio en tu navegador, se cierra la sesión y se pierden esos filtros guardados. No se pierde ninguna reserva.',
        ],
      },
      {
        id: 'conservacion',
        title: '7. Cuánto tiempo conservamos los datos',
        paragraphs: [
          'Los datos de cuenta se conservan mientras la cuenta esté activa. El historial de reservas y pagos se mantiene mientras sea necesario para justificar la operación y atender reclamaciones.',
          'Las reseñas siguen publicadas aunque cambies tu perfil, porque forman parte de la información pública de una experiencia; si eliminas tu cuenta, se disocian de tu identidad.',
        ],
      },
      {
        id: 'derechos',
        title: '8. Tus derechos',
        paragraphs: [
          'Puedes acceder a tus datos, rectificarlos, solicitar su eliminación y oponerte a determinados tratamientos.',
        ],
        items: [
          'Acceso y rectificación: la mayoría de los datos se editan directamente desde tu perfil.',
          'Contraseña: se cambia desde “Cambiar contraseña” o con el enlace de recuperación.',
          'Eliminación: solicítala por los canales indicados en la página de contacto, escribiendo desde el correo registrado.',
          'Notificaciones: desactiva las push desde los permisos del navegador en cualquier momento.',
        ],
      },
      {
        id: 'seguridad',
        title: '9. Seguridad',
        paragraphs: [
          'El tráfico viaja cifrado mediante HTTPS, las contraseñas se guardan con hash PBKDF2 y el acceso a cada recurso se valida por rol en el servidor, no solo en la interfaz.',
          'Ningún sistema es infalible. Si detectas una vulnerabilidad, comunícala al equipo antes de divulgarla públicamente.',
        ],
      },
      {
        id: 'menores',
        title: '10. Menores de edad',
        paragraphs: [
          'La plataforma está dirigida a mayores de 18 años. No solicitamos ni tratamos deliberadamente datos de menores; si detectamos una cuenta creada por un menor, se elimina.',
        ],
      },
      {
        id: 'cambios-privacidad',
        title: '11. Cambios en esta política',
        paragraphs: [
          'Si modificamos esta política, actualizaremos la fecha que aparece al inicio de la página. Cuando el cambio afecte de forma significativa al tratamiento de tus datos, lo avisaremos también por correo.',
        ],
      },
    ],
    related: ['terms', 'cancellations', 'contact'],
  },
  terms: {
    eyebrow: 'Términos de uso',
    title: 'Términos y condiciones',
    description: 'Reglas que aplican a quienes usan GoIsland como viajeros y a quienes publican experiencias como anfitriones.',
    updatedAt: '10 de agosto de 2026',
    notice: 'GoIsland es un prototipo universitario. Las publicaciones, reservas y pagos sirven para demostrar el funcionamiento del proyecto y no constituyen contratos comerciales reales.',
    sections: [
      {
        id: 'aceptacion',
        title: '1. Aceptación',
        paragraphs: [
          'Al crear una cuenta, publicar una experiencia o realizar una reserva en GoIsland aceptas estos términos y la Política de privacidad. Si no estás de acuerdo con alguno de sus puntos, no utilices la plataforma.',
          'Estos términos se rigen por las leyes de la República Dominicana.',
        ],
      },
      {
        id: 'que-es',
        title: '2. Qué es GoIsland y qué no es',
        paragraphs: [
          'GoIsland es un espacio donde anfitriones locales publican experiencias turísticas y las personas viajeras las reservan. Actuamos como intermediarios entre ambas partes.',
          'No somos el operador de las actividades. Quien publica una experiencia es responsable de prestarla, de contar con los permisos, seguros y habilitaciones que exija la normativa turística dominicana, y de cumplir lo que ofrece en su publicación.',
          'El contrato de la actividad se establece entre la persona viajera y el anfitrión.',
        ],
      },
      {
        id: 'cuenta',
        title: '3. Tu cuenta',
        items: [
          'Debes ser mayor de 18 años y proporcionar información veraz.',
          'Una persona, una cuenta. No se permite suplantar a terceros ni crear perfiles con datos de otros.',
          'Eres responsable de la actividad realizada desde tu cuenta y de custodiar tu contraseña.',
          'Si detectas un acceso no autorizado, cambia tu contraseña y comunícalo de inmediato.',
        ],
      },
      {
        id: 'anfitriones',
        title: '4. Obligaciones del anfitrión',
        paragraphs: [
          'El perfil de anfitrión se solicita desde la plataforma y queda sujeto a revisión. Una vez aprobado, quien publica se compromete a:',
        ],
        items: [
          'Describir la actividad con exactitud: incluido lo que cubre el precio, la duración, el punto de encuentro, el idioma, la dificultad y los requisitos de accesibilidad.',
          'Usar imágenes propias o con derechos suficientes, que correspondan a la actividad real.',
          'Mantener actualizados los horarios y los cupos disponibles.',
          'Responder a las solicitudes de cambio y cancelación en un plazo razonable.',
          'Cumplir la normativa aplicable en materia turística, laboral, sanitaria y de seguridad.',
        ],
      },
      {
        id: 'reservas',
        title: '5. Reservas, cupos y precios',
        paragraphs: [
          'Los precios se muestran por persona y en dólares estadounidenses (USD). El precio vigente es el que aparece en el momento de reservar.',
          'Al reservar, los cupos quedan retenidos en estado pendiente. Si el pago no se completa dentro del plazo indicado en la propia reserva, esta vence automáticamente y los cupos vuelven a estar disponibles para otras personas.',
          'Puedes solicitar un cambio de fecha o la cancelación desde el detalle de la reserva. La solicitud la resuelve el anfitrión: no es un cambio automático.',
        ],
      },
      {
        id: 'pagos',
        title: '6. Pagos',
        paragraphs: [
          'Los cobros se procesan a través de Stripe. Los datos de tarjeta se introducen en un formulario del propio Stripe: GoIsland no recibe, ve ni almacena números de tarjeta.',
          'Una reserva se considera confirmada cuando el proveedor de pago comunica que la transacción fue aprobada. Si el cobro es rechazado, la reserva permanece pendiente hasta que venza.',
        ],
      },
      {
        id: 'resenas',
        title: '7. Reseñas y contenido de usuarios',
        paragraphs: [
          'Solo puede reseñar una experiencia quien tenga una reserva completada sobre ella. Las reseñas deben describir la experiencia vivida.',
          'Al publicar contenido, nos autorizas a mostrarlo dentro de la plataforma. Conservas la titularidad de lo que escribes y de las imágenes que subes.',
          'El equipo puede ocultar reseñas que incumplan estas reglas, sin que ello suponga modificar la calificación de forma arbitraria.',
        ],
      },
      {
        id: 'prohibido',
        title: '8. Conductas prohibidas',
        items: [
          'Publicar contenido falso, ofensivo, discriminatorio o que vulnere derechos de terceros.',
          'Usar la plataforma para actividades ilícitas o para eludir obligaciones legales.',
          'Intentar acceder a cuentas ajenas, alterar el funcionamiento del servicio o extraer datos de forma automatizada. Estas conductas pueden constituir delito conforme a la Ley núm. 53-07 sobre crímenes y delitos de alta tecnología.',
          'Redirigir la reserva fuera de GoIsland para evitar los controles de la plataforma.',
          'Publicar reseñas a cambio de contraprestación o crear cuentas para inflar calificaciones.',
        ],
      },
      {
        id: 'moderacion',
        title: '9. Moderación, suspensión y cierre',
        paragraphs: [
          'Podemos despublicar experiencias, ocultar reseñas, suspender el perfil de anfitrión o cerrar cuentas que incumplan estos términos, especialmente cuando exista riesgo para otras personas usuarias.',
          'Puedes cerrar tu cuenta cuando quieras. Las reservas confirmadas y sus obligaciones se mantienen hasta que se resuelvan.',
        ],
      },
      {
        id: 'propiedad',
        title: '10. Propiedad intelectual',
        paragraphs: [
          'La marca GoIsland, el diseño de la interfaz y el código de la plataforma pertenecen al equipo del proyecto. No se permite reproducirlos ni crear servicios derivados sin autorización escrita.',
        ],
      },
      {
        id: 'responsabilidad',
        title: '11. Limitación de responsabilidad',
        paragraphs: [
          'GoIsland no responde por la calidad, seguridad ni legalidad de las actividades ofrecidas por los anfitriones, ni por los daños derivados de su prestación.',
          'Tampoco garantizamos que el servicio esté disponible de forma ininterrumpida. Podemos realizar tareas de mantenimiento que afecten temporalmente al acceso.',
          'Nada de lo anterior excluye los derechos que la legislación dominicana reconoce a las personas consumidoras.',
        ],
      },
      {
        id: 'cambios-terminos',
        title: '12. Cambios en los términos',
        paragraphs: [
          'Podemos actualizar estos términos para reflejar cambios en el servicio o en la normativa. La fecha de la última actualización aparece al inicio de la página; si continúas usando la plataforma tras un cambio, se entiende que lo aceptas.',
        ],
      },
      {
        id: 'ley',
        title: '13. Ley aplicable y reclamaciones',
        paragraphs: [
          'Estos términos se interpretan conforme a la legislación dominicana. Antes de acudir a otra instancia, escríbenos: la mayoría de los conflictos se resuelven directamente entre las partes.',
          'Las personas consumidoras conservan su derecho a acudir a las autoridades competentes en materia de protección al consumidor.',
        ],
      },
    ],
    related: ['privacy', 'cancellations', 'contact'],
  },
  cancellations: {
    eyebrow: 'Cancelaciones',
    title: 'Cancelaciones y reembolsos',
    description: 'Cómo cancelar una reserva, qué política aplica en cada caso y cómo se gestionan las devoluciones.',
    updatedAt: '10 de agosto de 2026',
    notice: 'En la demostración del proyecto los cobros se realizan en el entorno de pruebas de Stripe, por lo que las devoluciones no implican movimientos de dinero real.',
    sections: [
      {
        id: 'antes',
        title: '1. Revisa la política antes de reservar',
        paragraphs: [
          'Cada experiencia declara su propia política de cancelación y la muestra en la página de detalle, junto al precio y a la duración. Es el anfitrión quien la elige, así que puede variar de una actividad a otra aunque sean del mismo tipo.',
          'Si la política no te encaja, decídelo antes de reservar: una vez confirmada la reserva, aplica la que estaba publicada en ese momento.',
        ],
      },
      {
        id: 'politicas',
        title: '2. Qué significa cada política',
        items: [
          'Flexible: el anfitrión admite cancelaciones hasta poco antes de la actividad y devuelve el importe pagado.',
          'Moderada: se admite la cancelación con antelación; muy cerca de la fecha, el anfitrión puede retener parte del importe.',
          'Estricta: pensada para actividades con costes comprometidos por adelantado. La cancelación tardía normalmente no se reembolsa.',
        ],
        paragraphs: [
          'La página de la experiencia indica los plazos concretos. Ante cualquier duda, pregunta al anfitrión antes de reservar.',
        ],
      },
      {
        id: 'como-cancelar',
        title: '3. Cómo cancelar tu reserva',
        paragraphs: [
          'Entra en “Mis reservas”, abre el detalle de la reserva y solicita la cancelación indicando el motivo. La solicitud llega al anfitrión, que la aprueba o la rechaza, y recibirás una notificación con la respuesta.',
          'Mientras la solicitud está pendiente, la reserva mantiene su estado original y los cupos siguen retenidos.',
        ],
      },
      {
        id: 'pendientes',
        title: '4. Reservas pendientes de pago',
        paragraphs: [
          'Una reserva que aún no se ha pagado tiene un tiempo límite visible en su detalle. Si ese plazo vence, la reserva expira sola, los cupos se liberan y no se genera ningún cobro.',
          'No hace falta que hagas nada para cancelarla: basta con dejarla vencer. Si cambias de idea, puedes volver a reservar siempre que queden cupos.',
        ],
      },
      {
        id: 'anfitrion-cancela',
        title: '5. Si el anfitrión cancela',
        paragraphs: [
          'Un anfitrión puede cancelar una reserva confirmada por causas justificadas, como condiciones meteorológicas adversas o un problema de seguridad. En ese caso recibirás una notificación con el motivo y el importe pagado se devuelve íntegramente.',
          'Las cancelaciones reiteradas por parte de un anfitrión se revisan por el equipo de moderación y pueden derivar en la suspensión del perfil.',
        ],
      },
      {
        id: 'cambios',
        title: '6. Cambiar la fecha en lugar de cancelar',
        paragraphs: [
          'Si el problema es la fecha y no la actividad, pide un cambio en lugar de cancelar. Desde el detalle de la reserva puedes solicitar otro horario disponible; si el anfitrión lo aprueba, la reserva se traslada sin necesidad de un nuevo pago.',
        ],
      },
      {
        id: 'reembolsos',
        title: '7. Cómo se procesan las devoluciones',
        paragraphs: [
          'Las devoluciones se emiten siempre sobre el pago original, a través de Stripe y hacia el mismo medio con el que pagaste. No realizamos devoluciones por otros canales ni en efectivo.',
          'El importe devuelto queda registrado en el detalle de la reserva. El tiempo que tarda en reflejarse en tu estado de cuenta depende de tu banco emisor.',
        ],
      },
      {
        id: 'no-presentarse',
        title: '8. No presentarse',
        paragraphs: [
          'Si no acudes a la actividad y no la cancelaste antes, se considera una ausencia y normalmente no da derecho a devolución, con independencia de la política declarada.',
          'Cuando surge un imprevisto de última hora, avisa al anfitrión cuanto antes: muchas veces existe margen para reprogramar.',
        ],
      },
      {
        id: 'desacuerdos',
        title: '9. Si no estás de acuerdo con la decisión',
        paragraphs: [
          'Cuando una solicitud se rechaza y consideras que la política no se aplicó correctamente, escríbenos con el número de reserva y una explicación breve. El equipo revisa el caso y puede intervenir sobre el pago si corresponde.',
        ],
      },
    ],
    related: ['terms', 'privacy', 'contact'],
  },
} satisfies Record<PublicPageKey, InfoPage>;

const paths: Record<PublicPageKey, string> = {
  contact: '/contacto',
  privacy: '/privacidad',
  terms: '/terminos',
  cancellations: '/cancelaciones',
};

const relatedMeta: Record<PublicPageKey, { label: string; hint: string; icon: typeof FileText }> = {
  contact: { label: 'Contacto', hint: 'A quién escribir y con qué datos', icon: Mail },
  privacy: { label: 'Privacidad', hint: 'Qué datos tratamos y por qué', icon: ShieldCheck },
  terms: { label: 'Términos de uso', hint: 'Reglas para viajeros y anfitriones', icon: FileText },
  cancellations: { label: 'Cancelaciones', hint: 'Plazos, cambios y devoluciones', icon: TicketX },
};

export const PublicInfoPage = ({ page }: PublicInfoPageProps) => {
  const content: InfoPage = pages[page];
  const [visited, setVisited] = useState<{ page: PublicPageKey; id: string }>(
    { page, id: content.sections[0]?.id ?? '' },
  );
  const activeSection = visited.page === page ? visited.id : content.sections[0]?.id ?? '';

  usePageMetadata({
    title: `${content.title} | GoIsland`,
    description: content.description,
    path: paths[page],
  });

  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        const visible = entries
          .filter((entry) => entry.isIntersecting)
          .sort((first, second) => first.boundingClientRect.top - second.boundingClientRect.top)[0];
        if (visible) setVisited({ page, id: visible.target.id });
      },
      { rootMargin: '-96px 0px -60% 0px', threshold: 0 },
    );

    content.sections.forEach((section) => {
      const element = document.getElementById(section.id);
      if (element) observer.observe(element);
    });

    return () => observer.disconnect();
  }, [content, page]);

  return (
    <div className="public-info animate-fade-in">
      <header className="public-info__hero">
        <div className="public-info__hero-content">
          <span className="public-info__eyebrow">{content.eyebrow}</span>
          <h1>{content.title}</h1>
          <p>{content.description}</p>
          <p className="public-info__updated">Última actualización: {content.updatedAt}</p>
        </div>
      </header>

      <div className="container public-info__body">
        <aside className="public-info__aside" aria-label="Contenido de la página">
          <nav className="surface-panel public-info__toc">
            <h2 className="public-info__toc-title">En esta página</h2>
            <ol>
              {content.sections.map((section) => (
                <li key={section.id}>
                  <a
                    href={`#${section.id}`}
                    onClick={() => setVisited({ page, id: section.id })}
                    className={activeSection === section.id ? 'is-active' : undefined}
                    aria-current={activeSection === section.id ? 'true' : undefined}
                  >
                    {section.title}
                  </a>
                </li>
              ))}
            </ol>
          </nav>
        </aside>

        <div className="public-info__main">
          <Alert tone="info">{content.notice}</Alert>

          <div className="surface-panel public-info__content">
            {content.sections.map((section) => (
              <section key={section.id} id={section.id}>
                <h2>{section.title}</h2>
                {section.paragraphs?.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
                {section.items && (
                  <ul className="public-info__list">
                    {section.items.map((item) => <li key={item}>{item}</li>)}
                  </ul>
                )}
              </section>
            ))}
          </div>

          <section className="public-info__related" aria-labelledby="public-info-related">
            <h2 id="public-info-related">Documentos relacionados</h2>
            <div className="public-info__related-grid">
              {content.related.map((key) => {
                const meta = relatedMeta[key];
                const Icon = meta.icon;
                return (
                  <Link className="surface-card public-info__related-card" key={key} to={paths[key]}>
                    <span className="public-info__related-icon" aria-hidden="true"><Icon size={20} /></span>
                    <span>
                      <strong>{meta.label}</strong>
                      <small>{meta.hint}</small>
                    </span>
                    <ArrowRight size={17} aria-hidden="true" />
                  </Link>
                );
              })}
            </div>
          </section>

          <Link className="button-link button-link--outline" to="/">Volver al inicio</Link>
        </div>
      </div>
    </div>
  );
};

export default PublicInfoPage;
