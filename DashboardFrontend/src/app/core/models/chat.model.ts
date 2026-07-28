/**
 * Request/Response models for Chat features.
 * Maps to backend Features/Chat/ DTOs.
 */

/** ChatMessageDto — single chat message */
export interface ChatMessage {
  id: string;
  content: string;
  isFromAi: boolean;
  createdAt: string;
}

/** SendChatMessageCommand — POST /api/chat/send */
export interface SendMessageRequest {
  message: string;
  gameId?: string;
  conversationId?: string;
  // userId is auto-injected by auth.interceptor from AuthService
}

/** SendChatMessageCommandResponse — returned by POST /api/chat/send */
export interface SendMessageResponse {
  userMessage: ChatMessage;
  aiMessage: ChatMessage;
  sources: string[];
  conversationId: string;
}
