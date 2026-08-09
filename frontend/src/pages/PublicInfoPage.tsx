import { Link } from 'react-router-dom';
import { usePageMetadata } from '../hooks/usePageMetadata';

type PublicPageKey = 'contact' | 'privacy' | 'terms' | 'cancellations';

interface PublicInfoPageProps {
  page: PublicPageKey;
}

const pages = {
  contact: {
    eyebrow: 'Contacto',
    title: 'Estamos para orientarte',
    description: 'Encuentra rápidamente la ayuda adecuada para tu consulta.',
    sections: [
      {
        title: 'Ayuda con una reserva',
        paragraphs: [
          'Abre Mis reservas, selecciona la experiencia y revisa allí su estado, el pago y las opciones disponibles.',
          'Si necesitas cambiar o cancelar una visita, envía la solicitud desde el detalle de la reserva para que el anfitrión pueda atenderla.',
        ],
      },
      {
        title: 'Ayuda con tu cuenta',
        paragraphs: [
          'Desde tu perfil puedes actualizar tus datos y la contraseña. Si no puedes acceder, utiliza la opción Recuperar contraseña en la pantalla de inicio de sesión.',
        ],
      },
    ],
  },
  privacy: {
    eyebrow: 'Privacidad',
    title: 'Cómo cuidamos tus datos',
    description: 'Información clara sobre los datos necesarios para utilizar GoIsland.',
    sections: [
      {
        title: 'Información que utilizamos',
        paragraphs: [
          'Guardamos los datos de tu perfil, reservas y preferencias necesarios para ofrecer el servicio y mantener tu cuenta segura.',
          'Los datos de pago se procesan mediante el formulario seguro del proveedor de pagos y no se almacenan en GoIsland.',
        ],
      },
      {
        title: 'Uso y eliminación',
        paragraphs: [
          'Utilizamos la información para gestionar reservas, avisarte de cambios importantes y mejorar la experiencia. Puedes actualizar los datos de tu perfil desde tu cuenta.',
        ],
      },
    ],
  },
  terms: {
    eyebrow: 'Términos de uso',
    title: 'Uso de GoIsland',
    description: 'Condiciones básicas para utilizar GoIsland y reservar experiencias.',
    sections: [
      {
        title: 'Reservas y experiencias',
        paragraphs: [
          'Al reservar, confirma que la fecha, la cantidad de personas y el importe sean correctos. Cada experiencia es gestionada por su anfitrión.',
          'Puedes solicitar una cancelación desde el detalle de la reserva. Cuando requiera aprobación, verás su estado en la misma pantalla.',
        ],
      },
      {
        title: 'Uso responsable',
        paragraphs: [
          'No publiques contenido ofensivo, engañoso o que vulnere los derechos de otras personas. GoIsland puede retirar publicaciones que incumplan estas condiciones.',
        ],
      },
    ],
  },
  cancellations: {
    eyebrow: 'Cancelaciones',
    title: 'Cancelaciones y reembolsos',
    description: 'Consulta las opciones disponibles cuando tus planes cambien.',
    sections: [
      {
        title: 'Solicitar una cancelación',
        paragraphs: [
          'Abre el detalle de la reserva para consultar las acciones disponibles. Cuando la reserva requiere aprobación, el anfitrión recibirá tu solicitud y podrás seguir su estado desde la misma pantalla.',
          'Si corresponde un reembolso, GoIsland mostrará el importe y su progreso hasta que quede completado.',
        ],
      },
      {
        title: 'Reservas pendientes de pago',
        paragraphs: [
          'El plazo para pagar aparece junto a la reserva. Si vence, los cupos se liberan automáticamente y podrás elegir otra fecha disponible.',
        ],
      },
    ],
  },
} satisfies Record<PublicPageKey, {
  eyebrow: string;
  title: string;
  description: string;
  sections: { title: string; paragraphs: string[] }[];
}>;

const paths: Record<PublicPageKey, string> = {
  contact: '/contacto',
  privacy: '/privacidad',
  terms: '/terminos',
  cancellations: '/cancelaciones',
};

export const PublicInfoPage = ({ page }: PublicInfoPageProps) => {
  const content = pages[page];
  usePageMetadata({
    title: `${content.title} | GoIsland`,
    description: content.description,
    path: paths[page],
  });

  return (
    <div className="container public-info animate-fade-in">
      <header className="page-heading public-info__heading">
        <div>
          <span className="page-heading__eyebrow">{content.eyebrow}</span>
          <h1>{content.title}</h1>
          <p>{content.description}</p>
        </div>
      </header>

      <div className="surface-panel public-info__content">
        {content.sections.map((section) => (
          <section key={section.title}>
            <h2>{section.title}</h2>
            {section.paragraphs.map((paragraph) => <p key={paragraph}>{paragraph}</p>)}
          </section>
        ))}
      </div>

      <Link className="button-link button-link--outline" to="/">Volver al inicio</Link>
    </div>
  );
};

export default PublicInfoPage;
