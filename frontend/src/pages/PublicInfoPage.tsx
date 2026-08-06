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
    description: 'Información para comunicarte con el equipo responsable de GoIsland.',
    sections: [
      {
        title: 'Consultas sobre la demostración',
        paragraphs: [
          'GoIsland es un prototipo universitario. Durante una presentación, puedes comunicar cualquier consulta directamente al equipo expositor.',
          'No envíes información financiera, documentos personales ni datos sensibles.',
        ],
      },
    ],
  },
  privacy: {
    eyebrow: 'Privacidad',
    title: 'Cómo cuidamos tus datos',
    description: 'Resumen del uso de datos personales en el prototipo universitario GoIsland.',
    sections: [
      {
        title: 'Información que utilizamos',
        paragraphs: [
          'El prototipo puede guardar los datos de tu perfil y la información necesaria para crear reservas de demostración.',
          'Usa datos ficticios cuando participes en una prueba. No ingreses información financiera ni datos personales sensibles.',
        ],
      },
      {
        title: 'Uso y eliminación',
        paragraphs: [
          'La información se utiliza únicamente para mostrar las funciones de GoIsland. Puedes solicitar al equipo responsable que elimine los datos creados durante la demostración.',
        ],
      },
    ],
  },
  terms: {
    eyebrow: 'Términos de uso',
    title: 'Uso de GoIsland',
    description: 'Condiciones básicas para participar en la demostración universitaria de GoIsland.',
    sections: [
      {
        title: 'Una experiencia de demostración',
        paragraphs: [
          'GoIsland es un prototipo universitario. Las publicaciones, reservas y pagos mostrados sirven únicamente para demostrar el funcionamiento del proyecto.',
          'No se procesan pagos reales ni se formalizan compras, viajes o contratos comerciales.',
        ],
      },
      {
        title: 'Uso responsable',
        paragraphs: [
          'No publiques contenido ofensivo, engañoso o que vulnere los derechos de otras personas. El equipo puede retirar contenido de prueba que afecte la demostración.',
        ],
      },
    ],
  },
  cancellations: {
    eyebrow: 'Cancelaciones',
    title: 'Cancelaciones y reembolsos',
    description: 'Cómo funcionan las cancelaciones dentro de la demostración de GoIsland.',
    sections: [
      {
        title: 'Reservas de demostración',
        paragraphs: [
          'Cada experiencia muestra su política de cancelación antes de reservar. Las cancelaciones y los reembolsos que aparecen en GoIsland son simulados y no implican movimientos de dinero.',
          'Si una reserva pendiente vence antes de completar el pago de prueba, los cupos se liberan automáticamente y puedes intentar reservar otra vez.',
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
