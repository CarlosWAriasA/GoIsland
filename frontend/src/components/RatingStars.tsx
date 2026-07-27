import { Star } from 'lucide-react';

interface RatingStarsProps {
  value: number;
  size?: number;
}

export const RatingStars = ({ value, size = 16 }: RatingStarsProps) => {
  const rounded = Math.max(0, Math.min(5, Math.round(value)));

  return (
    <span className="rating-stars" role="img" aria-label={`${rounded} de 5 estrellas`}>
      {[1, 2, 3, 4, 5].map((position) => (
        <Star
          key={position}
          size={size}
          aria-hidden="true"
          className={position <= rounded ? 'rating-stars__star rating-stars__star--filled' : 'rating-stars__star'}
        />
      ))}
    </span>
  );
};

export default RatingStars;
