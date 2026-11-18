import { useEffect, useRef, useState, type FormEvent } from 'react'
import { supportApi, type ChatTurn } from '../api'

interface Message extends ChatTurn {
  durationMs?: number
}

const MAX_QUESTION_LENGTH = 500

const SUGGESTIONS = [
  'What can I use to cook food while camping?',
  'Which product helps me see at night?',
  'What should I wear if it rains on a hike?',
  'Do you sell mountain bikes?',
]

// Chat with the AI support assistant. The whole conversation is sent with every question,
// so follow-ups like "is it waterproof?" work. Nothing is stored on the server.
export function Support() {
  const [messages, setMessages] = useState<Message[]>([])
  const [question, setQuestion] = useState('')
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const endRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    endRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages, loading])

  const ask = async (text: string) => {
    const trimmed = text.trim()
    if (!trimmed || loading) return

    const history: ChatTurn[] = messages.map(({ role, content }) => ({ role, content }))
    setMessages((current) => [...current, { role: 'user', content: trimmed }])
    setQuestion('')
    setError(null)
    setLoading(true)

    try {
      const response = await supportApi.chat(trimmed, history)
      setMessages((current) => [
        ...current,
        { role: 'assistant', content: response.answer, durationMs: response.durationMs },
      ])
    } catch (err) {
      setError(err instanceof Error ? err.message : 'The assistant could not answer. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault()
    void ask(question)
  }

  const clearChat = () => {
    setMessages([])
    setError(null)
  }

  return (
    <div className="support">
      <section className="hero">
        <h1>Ask our AI assistant</h1>
        <p>
          Not sure what to buy? Ask in plain English. The assistant only answers from our real
          products and prices.
        </p>
      </section>

      <div className="card chat">
        <div className="chat-messages" aria-live="polite">
          {messages.length === 0 && !loading && (
            <div className="chat-empty">
              <p className="muted">Try one of these:</p>
              <div className="chat-suggestions">
                {SUGGESTIONS.map((suggestion) => (
                  <button key={suggestion} type="button" className="btn btn-ghost btn-sm" onClick={() => void ask(suggestion)}>
                    {suggestion}
                  </button>
                ))}
              </div>
            </div>
          )}

          {messages.map((message, index) => (
            <div key={index} className={`chat-message chat-${message.role}`}>
              <div className="chat-bubble">{message.content}</div>
              {message.durationMs !== undefined && (
                <span className="chat-meta">Answered in {(message.durationMs / 1000).toFixed(1)}s</span>
              )}
            </div>
          ))}

          {loading && (
            <div className="chat-message chat-assistant">
              <div className="chat-bubble chat-thinking" role="status">
                <span className="spinner" aria-hidden="true" />
                Thinking… (a local AI model can take a few seconds)
              </div>
            </div>
          )}

          <div ref={endRef} />
        </div>

        {error && <div className="alert alert-error chat-error">{error}</div>}

        <form className="chat-form" onSubmit={handleSubmit}>
          <input
            type="text"
            value={question}
            onChange={(event) => setQuestion(event.target.value)}
            placeholder="Ask about our products…"
            maxLength={MAX_QUESTION_LENGTH}
            disabled={loading}
            aria-label="Your question"
          />
          <button className="btn btn-primary" type="submit" disabled={loading || !question.trim()}>
            Send
          </button>
          {messages.length > 0 && (
            <button className="btn btn-ghost" type="button" onClick={clearChat} disabled={loading}>
              Clear
            </button>
          )}
        </form>
      </div>
    </div>
  )
}
