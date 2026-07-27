import { useEffect, useState } from 'react';

interface TypewriterProps {
  text: string;
  speed?: number;
  startDelay?: number;
  className?: string;
}

const prefersReducedMotion = () => typeof window !== 'undefined'
  && window.matchMedia('(prefers-reduced-motion: reduce)').matches;

export const Typewriter = ({ text, speed = 38, startDelay = 0, className = '' }: TypewriterProps) => {
  const [typed, setTyped] = useState(() => (prefersReducedMotion() ? text : ''));
  const [typedFor, setTypedFor] = useState(text);
  const done = typed.length === text.length;

  if (typedFor !== text) {
    setTypedFor(text);
    setTyped(prefersReducedMotion() ? text : '');
  }

  useEffect(() => {
    if (prefersReducedMotion()) return;

    let index = 0;
    let intervalId: number | undefined;

    const startId = window.setTimeout(() => {
      intervalId = window.setInterval(() => {
        index += 1;
        setTyped(text.slice(0, index));
        if (index >= text.length && intervalId) window.clearInterval(intervalId);
      }, speed);
    }, startDelay);

    return () => {
      window.clearTimeout(startId);
      if (intervalId) window.clearInterval(intervalId);
    };
  }, [text, speed, startDelay]);

  return (
    <span className={`typewriter ${className}`.trim()}>
      <span aria-hidden="true">{typed}</span>
      {!done && <span className="typewriter__caret" aria-hidden="true" />}
      <span className="visually-hidden">{text}</span>
    </span>
  );
};

export default Typewriter;
