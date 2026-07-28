import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import type { ChatMessage, SendMessageRequest, SendMessageResponse } from '../models/chat.model';

/**
 * Service for Chat / RAG features.
 * Maps to backend ChatController endpoints.
 * Automatically injects userId into send requests.
 */
@Injectable({ providedIn: 'root' })
export class ChatService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly baseUrl = `${environment.apiBaseUrl}/api/chat`;

  /**
   * Send a chat message and receive AI response.
   * POST /api/chat/send
   * userId is auto-injected from AuthService.
   */
  sendMessage(req: SendMessageRequest): Observable<SendMessageResponse> {
    const user = this.authService.currentUser();
    const payload = { ...req, userId: user?.id };
    return this.http.post<SendMessageResponse>(`${this.baseUrl}/send`, payload);
  }

  /**
   * Get conversation history for a specific chat session.
   * GET /api/chat/conversation/{conversationId}
   */
  getConversationHistory(conversationId: string): Observable<ChatMessage[]> {
    return this.http.get<ChatMessage[]>(`${this.baseUrl}/conversation/${conversationId}`);
  }

  /**
   * Get recent chat history for a user.
   * GET /api/chat/history/{userId}?pageSize=20
   */
  getChatHistory(userId: string, pageSize = 20): Observable<ChatMessage[]> {
    const params = new HttpParams().set('pageSize', pageSize.toString());
    return this.http.get<ChatMessage[]>(`${this.baseUrl}/history/${userId}`, { params });
  }
}
