import { AlertCircle, Compass, MapPin, Ship, TreePine, Utensils, Waves } from 'lucide-react';
import type { Experience } from '../types';

interface CardProps {
  experience: Experience;
}

const getCategoryIcon = (category: string) => {
  const iconProps = { size: 52, 'aria-hidden': true as const };

  switch (category.toLowerCase()) {
    case 'acuático':
    case 'acuatico':
      return <Waves {...iconProps} />;
    case 'cruceros':
      return <Ship {...iconProps} />;
    case 'gastronomía':
    case 'gastronomia':
      return <Utensils {...iconProps} />;
    case 'naturaleza':
      return <TreePine {...iconProps} />;
    default:
      return <Compass {...iconProps} />;
  }
};

const formatPrice = (price: number) => new Intl.NumberFormat('es-DO', {
  style: 'currency',
  currency: 'USD',
}).format(price);

export const Card = ({ experience }: CardProps) => {
  const {
    title,
    description,
    location,
    category,
    price,
    availableSpots,
    capacity,
  } = experience;
  const isLowAvailability = availableSpots > 0 && capacity > 0 && availableSpots / capacity <= 0.3;

  return (
    <article className="experience-card glass-card">
      <div className="experience-card__placeholder" role="img" aria-label={`Ilustración para ${category}`}>
        {getCategoryIcon(category)}
        <span>{category}</span>
      </div>

      <div className="experience-card__body">
        <div className="experience-card__location">
          <MapPin size={16} aria-hidden="true" />
          <span>{location}</span>
        </div>
        <h3>{title}</h3>
        <p>{description}</p>
      </div>

      <div className="experience-card__footer">
        <div>
          <span className="experience-card__label">Precio por persona</span>
          <strong>{formatPrice(price)}</strong>
        </div>
        <div className="experience-card__availability">
          {(availableSpots === 0 || isLowAvailability) && <AlertCircle size={16} aria-hidden="true" />}
          <span>
            {availableSpots === 0
              ? 'Sin cupos'
              : `${availableSpots} de ${capacity} cupos`}
          </span>
        </div>
      </div>
    </article>
  );
};

export default Card;
