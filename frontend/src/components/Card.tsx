import { AlertCircle, ArrowRight, Compass, MapPin, Ship, TreePine, Utensils, Waves } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { resolveApiAssetUrl } from '../services/api';
import type { Experience } from '../types';

interface CardProps {
  experience: Experience;
}

const getCategorySlug = (category: string) => {
  switch (category.toLowerCase()) {
    case 'acuático':
    case 'acuatico':
      return 'acuatico';
    case 'cruceros':
      return 'cruceros';
    case 'gastronomía':
    case 'gastronomia':
      return 'gastronomia';
    case 'naturaleza':
      return 'naturaleza';
    default:
      return 'default';
  }
};

const getCategoryIcon = (category: string) => {
  const iconProps = { size: 26, 'aria-hidden': true as const };

  switch (getCategorySlug(category)) {
    case 'acuatico':
      return <Waves {...iconProps} />;
    case 'cruceros':
      return <Ship {...iconProps} />;
    case 'gastronomia':
      return <Utensils {...iconProps} />;
    case 'naturaleza':
      return <TreePine {...iconProps} />;
    default:
      return <Compass {...iconProps} />;
  }
};

const formatPrice = (price: number) => price === 0
  ? 'Gratis'
  : new Intl.NumberFormat('es-DO', { style: 'currency', currency: 'USD' }).format(price);

export const Card = ({ experience }: CardProps) => {
  const currentLocation = useLocation();
  const {
    id,
    title,
    description,
    location,
    category,
    price,
    availableSpots,
    capacity,
    isUnlimitedCapacity,
    images,
  } = experience;
  const isLowAvailability = !isUnlimitedCapacity
    && availableSpots > 0 && capacity > 0 && availableSpots / capacity <= 0.3;
  const coverImage = images[0];

  return (
    <article className="experience-card surface-card">
      <div
        className={`experience-card__placeholder experience-card__placeholder--${coverImage ? 'image' : getCategorySlug(category)}`}
        role="img"
        aria-label={`Imagen de ambiente de la categoría ${category}`}
        style={coverImage ? { backgroundImage: `url("${resolveApiAssetUrl(coverImage.url)}")` } : undefined}
      >
        {!coverImage && getCategoryIcon(category)}
        <Link
          className="experience-card__image-link"
          to={`/experiences/${id}`}
          state={{ from: `${currentLocation.pathname}${currentLocation.search}` }}
          aria-label={`Ver detalles de ${title}`}
        />
        <Link
          className="experience-card__category"
          to={`/experiences?category=${encodeURIComponent(category)}`}
          aria-label={`Ver experiencias de la categoría ${category}`}
        >
          {category}
        </Link>
      </div>

      <div className="experience-card__body">
        <div className="experience-card__location">
          <MapPin size={16} aria-hidden="true" />
          <span>{location}</span>
        </div>
        <h3>
          <Link
            to={`/experiences/${id}`}
            state={{ from: `${currentLocation.pathname}${currentLocation.search}` }}
          >
            {title}
          </Link>
        </h3>
        <p>{description}</p>
        <Link
          className="experience-card__detail-link"
          to={`/experiences/${id}`}
          state={{ from: `${currentLocation.pathname}${currentLocation.search}` }}
          aria-label={`Ver detalles de ${title}`}
        >
          Ver detalles <ArrowRight size={17} aria-hidden="true" />
        </Link>
      </div>

      <div className="experience-card__footer">
        <div>
          <span className="experience-card__label">Precio por persona</span>
          <strong>{formatPrice(price)}</strong>
        </div>
        <div className="experience-card__availability">
          {(availableSpots === 0 || isLowAvailability) && <AlertCircle size={16} aria-hidden="true" />}
          <span>
            {isUnlimitedCapacity
              ? 'Sin límite de cupos'
              : availableSpots === 0
              ? 'Sin cupos'
              : `${availableSpots} de ${capacity} cupos`}
          </span>
        </div>
      </div>
    </article>
  );
};

export default Card;
