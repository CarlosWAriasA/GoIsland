import React, { Component } from 'react';
import type { ReactNode } from 'react';
import Button from './Button';

interface Props {
  children: ReactNode;
}

interface State {
  hasError: boolean;
}

export class ErrorBoundary extends Component<Props, State> {
  public state: State = {
    hasError: false,
  };

  public static getDerivedStateFromError(): State {
    return { hasError: true };
  }

  public componentDidCatch(error: Error, errorInfo: React.ErrorInfo): void {
    console.error('ErrorBoundary caught an error:', error, errorInfo);
  }

  public render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="container experience-detail-state animate-fade-in" style={{ padding: '4rem 1.5rem', textAlign: 'center' }}>
          <h2>Sesión finalizada</h2>
          <p>Se ha cerrado la sesión correctamente.</p>
          <div style={{ marginTop: '1.5rem' }}>
            <Button
              onClick={() => {
                this.setState({ hasError: false });
                window.location.href = '/login';
              }}
            >
              Ir a inicio de sesión
            </Button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}

export default ErrorBoundary;
