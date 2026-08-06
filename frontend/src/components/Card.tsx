import { AlertCircle, ArrowRight, Compass, MapPin, MapPinned, Ship, TreePine, Utensils, Waves } from 'lucide-react';
import { Link, useLocation } from 'react-router-dom';
import { resolveApiAssetUrl } from '../services/api';
import { formatLocationLabel } from '../services/googleMapsService';
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
    slug,
    title,
    shortDescription,
    description,
    location,
    category,
    price,
    availableSpots,
    capacity,
    isUnlimitedCapacity,
    images,
    latitude,
    longitude,
  } = experience;
  const isLowAvailability = !isUnlimitedCapacity
    && availableSpots > 0 && capacity > 0 && availableSpots / capacity <= 0.3;
  const coverImage = images.find((image) => image.isCover) ?? images[0];
  const detailState = { from: `${currentLocation.pathname}${currentLocation.search}` };

  return (
    <article className="experience-card surface-card">
      <Link
        className="experience-card__link"
        to={`/experiences/${slug}`}
        state={detailState}
        aria-label={`Ver detalles de ${title}`}
      >
        <div
          className={`experience-card__placeholder experience-card__placeholder--${coverImage ? 'image' : getCategorySlug(category)}`}
          role="img"
          aria-label={coverImage?.altText || `Imagen de ambiente de la categoría ${category}`}
          style={coverImage
            ? { backgroundImage: `url("${resolveApiAssetUrl(coverImage.cardUrl)}")` }
            : undefined}
        >
          {!coverImage && getCategoryIcon(category)}
          <span className="experience-card__category">{category}</span>
        </div>

        <div className="experience-card__body">
          <div className="experience-card__location">
            <MapPin size={16} aria-hidden="true" />
            <span>{formatLocationLabel(location)}</span>
          </div>
          <h3>{title}</h3>
          <p>{shortDescription || description}</p>
          <span className="experience-card__detail-link">
            Ver detalles <ArrowRight size={17} aria-hidden="true" />
          </span>
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
      </Link>
      {latitude !== null && longitude !== null && (
        <Link
          className="experience-card__map-link"
          to={`/experiences/map?experience=${experience.id}`}
          aria-label={`Ver ${title} en el mapa`}
        >
          <MapPinned size={17} aria-hidden="true" /> Ver en el mapa
        </Link>
      )}
    </article>
  );
};

export default Card;
