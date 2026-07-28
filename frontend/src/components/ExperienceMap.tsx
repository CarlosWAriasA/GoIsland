import L from 'leaflet';
import { useEffect, useRef } from 'react';

export interface MapPoint {
  id: number | string;
  title: string;
  latitude: number;
  longitude: number;
}

interface ExperienceMapProps {
  points?: MapPoint[];
  selectedPoint?: { latitude: number; longitude: number } | null;
  userPoint?: { latitude: number; longitude: number } | null;
  onSelect?: (point: { latitude: number; longitude: number }) => void;
  onPointClick?: (id: MapPoint['id']) => void;
  label: string;
}

const defaultCenter: L.LatLngExpression = [18.7357, -70.1627];

const markerIcon = L.divIcon({
  className: 'goisland-map-marker',
  html: '<span aria-hidden="true"></span>',
  iconAnchor: [14, 28],
  iconSize: [28, 28],
});

const selectedIcon = L.divIcon({
  className: 'goisland-map-marker goisland-map-marker--selected',
  html: '<span aria-hidden="true"></span>',
  iconAnchor: [14, 28],
  iconSize: [28, 28],
});

export const ExperienceMap = ({
  points = [],
  selectedPoint,
  userPoint,
  onSelect,
  onPointClick,
  label,
}: ExperienceMapProps) => {
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!containerRef.current) return;

    const map = L.map(containerRef.current, {
      center: defaultCenter,
      zoom: 8,
      scrollWheelZoom: false,
    });
    L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      maxZoom: 19,
    }).addTo(map);

    const bounds: L.LatLngExpression[] = [];
    points.forEach((point) => {
      const coordinates: L.LatLngExpression = [point.latitude, point.longitude];
      bounds.push(coordinates);
      const marker = L.marker(coordinates, {
        icon: markerIcon,
        keyboard: true,
        bubblingMouseEvents: false,
        title: point.title,
      }).addTo(map);
      const tooltip = document.createElement('span');
      tooltip.textContent = point.title;
      marker.bindTooltip(tooltip);
      if (onPointClick) {
        marker.on('click', () => onPointClick(point.id));
      }
    });

    if (selectedPoint) {
      const coordinates: L.LatLngExpression = [selectedPoint.latitude, selectedPoint.longitude];
      bounds.push(coordinates);
      L.marker(coordinates, {
        icon: selectedIcon,
        keyboard: false,
        interactive: false,
      }).addTo(map);
    }

    if (userPoint) {
      const coordinates: L.LatLngExpression = [userPoint.latitude, userPoint.longitude];
      bounds.push(coordinates);
      L.circleMarker(coordinates, {
        radius: 8,
        color: '#075985',
        fillColor: '#38bdf8',
        fillOpacity: 1,
        weight: 3,
      }).bindTooltip('Tu ubicación aproximada').addTo(map);
    }

    if (bounds.length === 1) {
      map.setView(bounds[0], 14);
    } else if (bounds.length > 1) {
      map.fitBounds(L.latLngBounds(bounds), { padding: [36, 36], maxZoom: 14 });
    }

    if (onSelect) {
      map.on('click', (event) => onSelect({
        latitude: Number(event.latlng.lat.toFixed(6)),
        longitude: Number(event.latlng.lng.toFixed(6)),
      }));
    }

    window.setTimeout(() => map.invalidateSize(), 0);
    return () => {
      map.remove();
    };
  }, [onPointClick, onSelect, points, selectedPoint, userPoint]);

  return <div className="experience-map" ref={containerRef} role="application" aria-label={label} />;
};

export default ExperienceMap;
