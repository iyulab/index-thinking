# IndexThinking

> **A turn-management library that orchestrates LLM reasoning segments to ensure seamless long-form responses and logical continuity.**

`index-thinking` is a specialized "Working Memory" manager designed for **Reasoning-capable LLMs**. It manages the lifecycle of a single conversation turn by segmenting thought processes, handling token-limit interruptions, and ensuring that deep research tasks remain logically consistent across automated continuations—**regardless of whether the model is hosted locally or accessed via global Cloud APIs.**

## 🌟 Core Concept

While **[Memory-Indexer](https://github.com/iyulab/memory-indexer)** handles **Long-term Memory** (retrieving past knowledge), **IndexThinking** handles **Working Memory** (managing the current flow of thought). It addresses the "Stateless" nature of LLM APIs by providing a "Stateful" layer that tracks reasoning steps and output fragments.

## ✨ Key Features

### 1. Model-Agnostic Thinking Segmentation

Compatible with any model that outputs reasoning traces (e.g., `<thought>` tags, `reasoning_content` fields, or internal chain-of-thought). It parses and separates these segments in real-time.

### 2. Universal Stateful Continuation

Solves the "Truncated Output" problem common in all major APIs (OpenAI, Gemini, Anthropic, and various Open-Source models). It takes a "contextual snapshot" of the truncated state and bridges the gap seamlessly via automated "Resume" triggers.

### 3. Multi-Step Research Orchestration

Ideal for long-form research tasks that exceed a single API call's output limit. It breaks complex queries into logical steps and saves checkpoints, enabling stable, deep-dive analysis.

### 4. Dynamic Token Budgeting

Optimizes the balance between "Thinking" and "Answering." It prevents models from exhausting the output token limit on reasoning alone, ensuring there is always room for the final conclusion.

## 🚀 Comparison: Memory-Indexer vs. IndexThinking

| Feature | **Memory-Indexer** | **IndexThinking** |
| --- | --- | --- |
| **Perspective** | Long-term Memory (Past) | Working Memory (Present) |
| **Data Focus** | Past conversations, Knowledge Base | Current thought process, Turn state |
| **System Role** | Librarian (Search & Retrieve) | Architect (Plan & Execute) |
| **Primary Goal** | "What do we know?" | "Where are we in this task?" |

## 🛠 Target Environment

`index-thinking` is designed to be provider-agnostic, supporting:

* **Cloud APIs**: OpenAI o1/o3, Gemini 2.0 Thinking, Claude 3.5, etc.
* **Inference Engines**: GPUStack, vLLM, Ollama, DeepSpeed, etc.
* **Orchestrators**: LangChain, LlamaIndex, or custom backend stacks.
