import {
  Component,
  ElementRef,
  ViewChild,
  inject,
  signal,
  computed,
  OnInit,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ChatService } from '../../../core/services/chat.service';
import { GameService } from '../../../core/services/game.service';
import { AuthService } from '../../../core/services/auth.service';
import type { ChatMessage, SendMessageRequest } from '../../../core/models/chat.model';
import type { Game } from '../../../core/models/game.model';

/**
 * ChatContainerComponent — RAG chatbot interface with source citations.
 *
 * Features:
 * - Multi-turn conversation with AI
 * - Game context selector (for RAG)
 * - Source citations display
 * - Auto-scroll to latest message
 * - Loading state during AI response
 *
 * Follows Angular Signals best practices from .claude/skills/angular-signals.md
 */
@Component({
  selector: 'app-chat-container',
  imports: [FormsModule],
  templateUrl: './chat-container.component.html',
  styleUrl: './chat-container.component.css',
})
export class ChatContainerComponent implements OnInit {
  @ViewChild('messageContainer') private messageContainer!: ElementRef<HTMLDivElement>;
  @ViewChild('messageInput') messageInput?: HTMLTextAreaElement;

  // ── Services (using inject()) ──────────────────────────────────
  private readonly chatService = inject(ChatService);
  private readonly gameService = inject(GameService);
  readonly authService = inject(AuthService);

  // ── Writable Signals (private, use .update/.set) ────────────────
  private readonly _messages = signal<ChatMessage[]>([]);
  private readonly _sources = signal<string[]>([]);
  private readonly _games = signal<Game[]>([]);
  private readonly _selectedGameId = signal<string | undefined>(undefined);
  private readonly _isLoading = signal(false);
  private readonly _error = signal<string | null>(null);
  private readonly _messageText = signal('');
  private readonly _conversationId = signal<string | undefined>(undefined);

  // ── Readonly Signals (expose to template) ──────────────────────
  readonly messages = this._messages.asReadonly();
  readonly sources = this._sources.asReadonly();
  readonly games = this._games.asReadonly();
  readonly selectedGameId = this._selectedGameId.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly messageText = this._messageText.asReadonly();
  readonly conversationId = this._conversationId.asReadonly();

  // ── Computed Signals ───────────────────────────────────────────
  readonly hasMessages = computed(() => this._messages().length > 0);

  /**
   * Get the current user's display name initial.
   */
  readonly currentUserDisplayName = computed(() => {
    const name = this.authService.currentUser()?.displayName;
    return name?.[0]?.toUpperCase() || 'U';
  });

  // ── Lifecycle ──────────────────────────────────────────────────
  ngOnInit(): void {
    this.loadGames();
  }

  // ── Public Methods (called from template) ──────────────────────

  /**
   * Set message text and focus the input.
   */
  setMessageAndFocus(text: string): void {
    this._messageText.set(text);
    // Focus after view update
    setTimeout(() => this.messageInput?.focus(), 0);
  }

  /**
   * Set message text (called from template binding).
   */
  setMessageText(text: string): void {
    this._messageText.set(text);
  }

  /**
   * Send a message to the AI.
   */
  sendMessage(): void {
    const text = this._messageText().trim();
    if (!text || this._isLoading()) return;

    // Add user message immediately
    const userMessage: ChatMessage = {
      id: crypto.randomUUID(),
      content: text,
      isFromAi: false,
      createdAt: new Date().toISOString(),
    };
    this._messages.update((msgs) => [...msgs, userMessage]);

    // Clear input
    this._messageText.set('');
    this._error.set(null);

    // Scroll to bottom
    setTimeout(() => this.scrollToBottom(), 0);

    // Send to backend
    const request: SendMessageRequest = {
      message: text,
      gameId: this._selectedGameId(),
      conversationId: this._conversationId(),
    };

    this._isLoading.set(true);

    this.chatService.sendMessage(request).subscribe({
      next: (response) => {
        // Add AI response
        this._messages.update((msgs) => [...msgs, response.aiMessage]);
        this._sources.set(response.sources);
        this._conversationId.set(response.conversationId);
        this._isLoading.set(false);

        // Scroll to bottom
        setTimeout(() => this.scrollToBottom(), 0);
      },
      error: (err: { detail?: string; title?: string }) => {
        this._isLoading.set(false);
        this._error.set(err.detail || err.title || 'Failed to send message');
      },
    });
  }

  /**
   * Handle Enter key (Shift+Enter for new line).
   */
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.sendMessage();
    }
  }

  /**
   * Auto-resize textarea height.
   */
  autoResize(event: Event): void {
    const textarea = event.target as HTMLTextAreaElement;
    textarea.style.height = 'auto';
    textarea.style.height = `${Math.min(textarea.scrollHeight, 150)}px`;
  }

  /**
   * Clear chat history.
   */
  clearChat(): void {
    this._messages.set([]);
    this._sources.set([]);
    this._conversationId.set(undefined);
    this._error.set(null);
  }

  /**
   * Select a game for context.
   */
  selectGame(gameId: string | undefined): void {
    this._selectedGameId.set(gameId);
  }

  /**
   * Format timestamp for display.
   */
  formatTime(isoString: string): string {
    const date = new Date(isoString);
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  // ── Private Methods ────────────────────────────────────────────

  /**
   * Load available games for context selection.
   */
  private loadGames(): void {
    this.gameService.getGames(1, 100).subscribe({
      next: (response) => {
        this._games.set(response.items);
      },
      error: (err: unknown) => {
        console.error('Failed to load games:', err);
      },
    });
  }

  /**
   * Scroll message container to bottom.
   */
  private scrollToBottom(): void {
    if (this.messageContainer?.nativeElement) {
      this.messageContainer.nativeElement.scrollTop =
        this.messageContainer.nativeElement.scrollHeight;
    }
  }
}
