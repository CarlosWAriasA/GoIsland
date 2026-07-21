interface SkeletonProps {
  className?: string;
}

export const Skeleton = ({ className = '' }: SkeletonProps) => (
  <div className={`skeleton ${className}`} aria-hidden="true" />
);

export default Skeleton;
