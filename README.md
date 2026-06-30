# Omoi #
v1.6

**Omoi** is an experimental chatbot intended for casual, speculative and intellectual conversation. It uses *modes* to promote flexibility and provide a responsive style of speech and analysis. Mode determines the system prompt for the current generation. The aim of Omoi is to serve as an alternative to the increasingly task- and information-focused styles of the official chatbots.

Omoi is built in Avalonia and uses OpenRouter-provided models to converse, think about messages, summarize and store memories. All generation and data storage uses external APIs. The application must be configured with an OpenRouter API key before it can be used (Settings → Integrations). Omoi works with the user's own account and token budget.

Optional vector memory can be configured using:
- **Qdrant** (recommended) - Supports `text-embedding-3-large` model with 3072 dimensions for maximum quality
- **Supabase** - Supports `text-embedding-3-small` model with 1536 dimensions (limited by pgvector constraints)

Omoi employs a chain of micro-prompts to classify the most recent message for mode selection; the configured Thought model is used for this. The console (View -> Show Thought Process) displays these in real-time, as well as system prompts and the memory storage process. Current available modes are Investigate, Opine and Talk; these are identified in the client UI by a kanji sign. Most of these modes use agentic patterns to produce their responses; I sought to design a bot that is comfortable being opinionated or temporarily off-topic.

The conversation context is 8 exchanges plus any retrieved memories. 

Future feature work will include a Google Drive integration to sync conversations across devices, mobile support, and markdown (pending a stable version of `Markdown.Avalonia` version 11.0.3).

The behavior of modes and memory are in active development so check back for updates!

## Recommended settings
For best quality the following models are currently recommended: Google Gemini Pro Latest (Chat), Google Gemini 2.5 Flash (Thought), Claude Sonnet 4 (Memory), OpenAI Text Embedding 3 Small (Vector).

## Version history
1.0 - Interface, basic chat
 
1.1 - OpenAI models, improved mode prompts

1.2 - RAG, DeepSeek models

1.3 - Agent patterns for modes, refactor

1.4 - Experimentation with classifier prompts

1.5 - Replace provider clients with OpenRouter client

1.6 - Salience-based classification
