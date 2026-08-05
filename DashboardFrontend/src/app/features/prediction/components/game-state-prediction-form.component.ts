import { Component, input, output, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { GameStatePredictionInput } from '../../../core/models/prediction.model';
import { DEFAULT_PREDICTION_INPUT, PREDICTION_VALIDATION } from '../../../core/models/prediction.model';

/**
 * GameStatePredictionFormComponent — Form for entering game state features.
 *
 * Features:
 * - Reactive form with validation
 * - Numeric inputs for all prediction features
 * - Loading state during submission
 *
 * Follows Angular Signals best practices.
 */
@Component({
  selector: 'app-game-state-prediction-form',
  imports: [ReactiveFormsModule, LoadingSpinnerComponent],
  templateUrl: './game-state-prediction-form.component.html',
  styleUrl: './game-state-prediction-form.component.css',
})
export class GameStatePredictionFormComponent implements OnInit {
  // ── Inputs/Outputs ────────────────────────────────────────────
  readonly isLoading = input(false);
  readonly submitPrediction = output<GameStatePredictionInput>();

  // ── Services ─────────────────────────────────────────────────────
  private readonly fb = inject(FormBuilder);

  // ── Form ────────────────────────────────────────────────────────
  form!: FormGroup;

  // ── Lifecycle ──────────────────────────────────────────────────
  ngOnInit(): void {
    this.initForm();
  }

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Handle form submission.
   */
  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.value as Partial<GameStatePredictionInput>;
    const input: GameStatePredictionInput = {
      playerCount: Number(value.playerCount),
      hourOfDay: Number(value.hourOfDay),
      dayOfWeek: Number(value.dayOfWeek),
      heroLevel: Number(value.heroLevel),
      heroKills: Number(value.heroKills),
      deaths: Number(value.deaths),
      unitKills: Number(value.unitKills),
      totalGold: Number(value.totalGold),
      highestAtk: Number(value.highestAtk),
      highestDef: Number(value.highestDef),
      highestSpeed: Number(value.highestSpeed),
      atkRange: Number(value.atkRange),
    };

    this.submitPrediction.emit(input);
  }

  /**
   * Reset form to default values.
   */
  resetForm(): void {
    this.form.reset({
      playerCount: DEFAULT_PREDICTION_INPUT.playerCount,
      hourOfDay: new Date().getHours(),
      dayOfWeek: new Date().getDay(),
      heroLevel: DEFAULT_PREDICTION_INPUT.heroLevel,
      heroKills: DEFAULT_PREDICTION_INPUT.heroKills,
      deaths: DEFAULT_PREDICTION_INPUT.deaths,
      unitKills: DEFAULT_PREDICTION_INPUT.unitKills,
      totalGold: DEFAULT_PREDICTION_INPUT.totalGold,
      highestAtk: DEFAULT_PREDICTION_INPUT.highestAtk,
      highestDef: DEFAULT_PREDICTION_INPUT.highestDef,
      highestSpeed: DEFAULT_PREDICTION_INPUT.highestSpeed,
      atkRange: DEFAULT_PREDICTION_INPUT.atkRange,
    });
  }

  /**
   * Check if a field has an error.
   */
  hasError(field: string, errorCode: string): boolean {
    const control = this.form.get(field);
    return !!(control?.touched && control?.hasError(errorCode));
  }

  // ── Private Methods ────────────────────────────────────────────

  private initForm(): void {
    const v = PREDICTION_VALIDATION;
    const defaults = DEFAULT_PREDICTION_INPUT;
    this.form = this.fb.group({
      hourOfDay: [defaults.hourOfDay, [Validators.required, Validators.min(0), Validators.max(23)]],
      dayOfWeek: [defaults.dayOfWeek, [Validators.required, Validators.min(0), Validators.max(6)]],
      heroLevel: [defaults.heroLevel, [Validators.required, Validators.min(v.heroLevel.min), Validators.max(v.heroLevel.max)]],
      heroKills: [defaults.heroKills, [Validators.required, Validators.min(v.heroKills.min), Validators.max(v.heroKills.max)]],
      deaths: [defaults.deaths, [Validators.required, Validators.min(v.deaths.min), Validators.max(v.deaths.max)]],
      totalGold: [defaults.totalGold, [Validators.required, Validators.min(v.totalGold.min), Validators.max(v.totalGold.max)]],
      unitKills: [defaults.unitKills, [Validators.required, Validators.min(v.unitKills.min), Validators.max(v.unitKills.max)]],
      highestAtk: [defaults.highestAtk, [Validators.required, Validators.min(v.highestAtk.min), Validators.max(v.highestAtk.max)]],
      highestDef: [defaults.highestDef, [Validators.required, Validators.min(v.highestDef.min), Validators.max(v.highestDef.max)]],
      highestSpeed: [defaults.highestSpeed, [Validators.required, Validators.min(v.highestSpeed.min), Validators.max(v.highestSpeed.max)]],
      atkRange: [defaults.atkRange, [Validators.required, Validators.min(v.atkRange.min), Validators.max(v.atkRange.max)]],
      playerCount: [defaults.playerCount, [Validators.required, Validators.min(v.playerCount.min), Validators.max(v.playerCount.max)]],
    });
  }
}
