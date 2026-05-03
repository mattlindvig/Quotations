import React, { useState, useRef, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { sendChatMessage, type ChatMessage } from '../../services/chatService';
import type { Quotation } from '../../types/quotation';
import './ChatWidget.css';

interface DisplayMessage {
  role: 'user' | 'assistant';
  content: string;
  quotations?: Quotation[];
}

export const ChatWidget: React.FC = () => {
  const [isOpen, setIsOpen] = useState(false);
  const [input, setInput] = useState('');
  const [messages, setMessages] = useState<DisplayMessage[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);
  const navigate = useNavigate();

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  useEffect(() => {
    if (isOpen) inputRef.current?.focus();
  }, [isOpen]);

  // Close on Escape key
  useEffect(() => {
    if (!isOpen) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') setIsOpen(false);
    };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [isOpen]);

  const buildHistory = (): ChatMessage[] =>
    messages.map((m) => ({ role: m.role, content: m.content }));

  const handleSend = async () => {
    const text = input.trim();
    if (!text || isLoading) return;

    const userMessage: DisplayMessage = { role: 'user', content: text };
    setMessages((prev) => [...prev, userMessage]);
    setInput('');
    setIsLoading(true);

    try {
      const result = await sendChatMessage(text, buildHistory());
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: result.reply, quotations: result.quotations }
      ]);
    } catch {
      setMessages((prev) => [
        ...prev,
        { role: 'assistant', content: 'Sorry, something went wrong. Please try again.' }
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleQuoteClick = (id: string) => {
    navigate(`/quote/${id}`);
    setIsOpen(false);
  };

  return (
    <>
      {isOpen && (
        <div
          className="chat-overlay"
          role="dialog"
          aria-modal="true"
          aria-label="Quote finder chat"
          onClick={(e) => { if (e.target === e.currentTarget) setIsOpen(false); }}
        >
          <div className="chat-modal">
            <div className="chat-header">
              <span className="chat-title">Quote Finder</span>
              <button
                className="chat-close-btn"
                onClick={() => setIsOpen(false)}
                aria-label="Close chat"
              >
                ✕
              </button>
            </div>

            <div className="chat-messages">
              {messages.length === 0 && (
                <div className="chat-welcome">
                  <p>Ask me to find quotes! Try:</p>
                  <ul>
                    <li>"Find me quotes about courage"</li>
                    <li>"Show me quotes from books by Einstein"</li>
                    <li>"Give me a random quote"</li>
                  </ul>
                </div>
              )}

              {messages.map((msg, i) => (
                <div key={i} className={`chat-message chat-message--${msg.role}`}>
                  <div className="chat-bubble">{msg.content}</div>
                  {msg.quotations && msg.quotations.length > 0 && (
                    <div className="chat-quotations">
                      {msg.quotations.map((q) => (
                        <div
                          key={q.id}
                          className="chat-quote-card"
                          onClick={() => handleQuoteClick(q.id)}
                          role="button"
                          tabIndex={0}
                          onKeyDown={(e) => e.key === 'Enter' && handleQuoteClick(q.id)}
                          title="Click to view full quote"
                        >
                          <blockquote className="chat-quote-text">"{q.text}"</blockquote>
                          <div className="chat-quote-meta">
                            <span className="chat-quote-author">— {q.author.name}</span>
                            <span className="chat-quote-source">{q.source.title}</span>
                          </div>
                          {q.tags.length > 0 && (
                            <div className="chat-quote-tags">
                              {q.tags.slice(0, 3).map((tag) => (
                                <span key={tag} className="chat-quote-tag">{tag}</span>
                              ))}
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              ))}

              {isLoading && (
                <div className="chat-message chat-message--assistant">
                  <div className="chat-bubble chat-bubble--loading">
                    <span className="chat-dots">
                      <span /><span /><span />
                    </span>
                  </div>
                </div>
              )}

              <div ref={messagesEndRef} />
            </div>

            <div className="chat-input-area">
              <textarea
                ref={inputRef}
                className="chat-input"
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="Ask about quotes... (Enter to send, Shift+Enter for new line)"
                rows={2}
                disabled={isLoading}
                aria-label="Chat message input"
              />
              <button
                className="chat-send-btn"
                onClick={handleSend}
                disabled={!input.trim() || isLoading}
                aria-label="Send message"
              >
                ↑
              </button>
            </div>
          </div>
        </div>
      )}

      <button
        className={`chat-toggle-btn${isOpen ? ' chat-toggle-btn--open' : ''}`}
        onClick={() => setIsOpen((prev) => !prev)}
        aria-label={isOpen ? 'Close quote finder' : 'Open quote finder'}
        aria-expanded={isOpen}
      >
        {isOpen ? '✕' : '💬'}
      </button>
    </>
  );
};
