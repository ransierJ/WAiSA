import { useEffect, useRef } from 'react';

interface ActivityLog {
  timestamp: string;
  type: string;
  message: string;
  icon: string;
  details?: string;
}

interface ActivityPanelProps {
  activities: ActivityLog[];
  isActive: boolean;
}

export function ActivityPanel({ activities, isActive }: ActivityPanelProps) {
  const scrollRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to bottom when new activities arrive
  useEffect(() => {
    if (scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
    }
  }, [activities]);

  if (!isActive || activities.length === 0) {
    return null;
  }

  return (
    <div className="h-full flex flex-col bg-gray-50 border-l border-gray-200">
      <div className="p-4 border-b border-gray-200 bg-white">
        <h3 className="text-sm font-semibold text-gray-700">AI Activity</h3>
        <p className="text-xs text-gray-500 mt-1">Watching WAiSA think...</p>
      </div>

      <div
        ref={scrollRef}
        className="flex-1 overflow-y-auto p-4 space-y-3"
      >
        {activities.map((activity, index) => (
          <div
            key={index}
            className="flex items-start gap-3 animate-fade-in"
          >
            <div className="flex-shrink-0 text-2xl">
              {activity.icon}
            </div>
            <div className="flex-1 min-w-0">
              <div className="text-sm text-gray-800">
                {activity.message}
              </div>
              {activity.details && (
                <div className="text-xs text-gray-500 mt-1 font-mono bg-gray-100 p-2 rounded overflow-x-auto">
                  {activity.details}
                </div>
              )}
              <div className="text-xs text-gray-400 mt-1">
                {new Date(activity.timestamp).toLocaleTimeString()}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
