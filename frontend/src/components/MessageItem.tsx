import type { Message } from '../types';

interface MessageItemProps {
  message: Message;
}

export const MessageItem = ({ message }: MessageItemProps) => {
  const isUser = message.role === 'user';

  return (
    <div className={`flex ${isUser ? 'justify-end' : 'justify-start'} mb-4`}>
      <div className={`max-w-[80%] ${isUser ? 'bg-blue-500 text-white' : 'bg-white text-gray-900'} rounded-2xl px-4 py-3 shadow-sm`}>
        <div className="text-sm font-medium mb-1">
          {isUser ? 'You' : 'WAiSA'}
        </div>
        <div className="text-sm whitespace-pre-wrap">{message.content}</div>

        {message.commands && message.commands.length > 0 && (
          <div className="mt-3 space-y-2">
            {message.commands.map((cmd, idx) => {
              const isPending = cmd.output?.includes('⏳ Pending approval');
              const isQueued = cmd.output?.includes('✓ Queued for execution');
              const isExecuted = !isPending && !isQueued;

              let bgColor = 'bg-gray-50';
              let textColor = 'text-gray-900';
              let borderColor = 'border-gray-200';

              if (isPending) {
                bgColor = 'bg-yellow-50';
                textColor = 'text-yellow-900';
                borderColor = 'border-yellow-200';
              } else if (isQueued) {
                bgColor = 'bg-blue-50';
                textColor = 'text-blue-900';
                borderColor = 'border-blue-200';
              } else if (cmd.success) {
                bgColor = 'bg-green-50';
                textColor = 'text-green-900';
                borderColor = 'border-green-200';
              } else {
                bgColor = 'bg-red-50';
                textColor = 'text-red-900';
                borderColor = 'border-red-200';
              }

              return (
                <div
                  key={idx}
                  className={`text-xs p-3 rounded-lg border ${bgColor} ${textColor} ${borderColor}`}
                >
                  <div className="font-mono font-semibold mb-1">
                    {isPending && '⏳ '}
                    {isQueued && '✓ '}
                    {isExecuted && cmd.success && '✅ '}
                    {isExecuted && !cmd.success && '❌ '}
                    $ {cmd.command}
                  </div>
                  {cmd.output && (
                    <div className="mt-2 text-xs">
                      <div className="font-medium mb-1">Output:</div>
                      <pre className="overflow-x-auto bg-white bg-opacity-50 p-2 rounded">
                        {cmd.output}
                      </pre>
                    </div>
                  )}
                  {cmd.errorMessage && (
                    <div className="mt-2">
                      <div className="font-medium mb-1">Error:</div>
                      <div className="bg-white bg-opacity-50 p-2 rounded">
                        {cmd.errorMessage}
                      </div>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        )}

        <div className="text-xs opacity-60 mt-2">
          {new Date(message.timestamp).toLocaleTimeString()}
        </div>
      </div>
    </div>
  );
};
