import { Component, type ErrorInfo, type ReactNode } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';

interface Props {
  readonly children: ReactNode;
  readonly fallbackTitle?: string;
}

interface State {
  readonly hasError: boolean;
  readonly error: Error | null;
}

export class ErrorBoundary extends Component<Props, State> {
  public override state: State = {
    hasError: false,
    error: null,
  };

  public static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  public override componentDidCatch(error: Error, errorInfo: ErrorInfo): void {
    console.error('Unhandled UI error in component tree:', error, errorInfo);
  }

  private handleReset = (): void => {
    this.setState({ hasError: false, error: null });
  };

  public override render(): ReactNode {
    if (this.state.hasError) {
      return (
        <div className="card" style={{ borderLeft: '4px solid #ef4444', marginTop: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10, color: '#b91c1c' }}>
            <AlertTriangle size={22} />
            <h3 style={{ margin: 0 }}>{this.props.fallbackTitle ?? 'An unexpected error occurred in this view'}</h3>
          </div>
          <p style={{ marginTop: 8, color: '#475569' }}>
            {this.state.error?.message ?? 'A rendering error occurred. You can retry or switch tabs.'}
          </p>
          <button
            type="button"
            className="btn btn-secondary"
            style={{ marginTop: 12, display: 'inline-flex', alignItems: 'center', gap: 6 }}
            onClick={this.handleReset}
          >
            <RefreshCw size={14} /> Retry View
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}
