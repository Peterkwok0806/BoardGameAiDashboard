import { Component, input, output, signal, computed, OnInit, inject } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { NgChartsModule } from 'ng2-charts';
import { ChartConfiguration, ChartData, ChartOptions } from 'chart.js';
import { LoadingSpinnerComponent } from '../../../shared/components/loading-spinner/loading-spinner.component';
import type { LevelAnalysisResult } from '../../../core/models/prediction.model';
import { DEFAULT_PREDICTION_INPUT } from '../../../core/models/prediction.model';

/**
 * LevelAnalysisChartComponent — Displays win rate analysis across hero levels.
 *
 * Features:
 * - Line chart showing win probability vs hero level
 * - Adjustable base parameters for analysis
 * - Real-time chart updates
 *
 * Follows Angular Signals best practices.
 */
@Component({
  selector: 'app-level-analysis-chart',
  imports: [ReactiveFormsModule, NgChartsModule, LoadingSpinnerComponent],
  templateUrl: './level-analysis-chart.component.html',
  styleUrl: './level-analysis-chart.component.css',
})
export class LevelAnalysisChartComponent implements OnInit {
  // ── Inputs/Outputs ────────────────────────────────────────────
  readonly data = input<LevelAnalysisResult | null>(null);
  readonly isLoading = input(false);
  readonly analyze = output<{
    heroLevel?: number;
    heroKills?: number;
    deaths?: number;
    totalGold?: number;
    unitKills?: number;
    highestAtk?: number;
    highestDef?: number;
    highestSpeed?: number;
    playerCount?: number;
  }>();

  // ── Services ─────────────────────────────────────────────────────
  private readonly fb = inject(FormBuilder);

  // ── Form ────────────────────────────────────────────────────────
  form!: FormGroup;

  // ── Chart Data ─────────────────────────────────────────────────
  readonly chartData = computed<ChartData<'line'>>(() => {
    const analysisData = this.data();
    if (!analysisData || !analysisData.heroLevels.length) {
      return {
        labels: [],
        datasets: [],
      };
    }

    return {
      labels: analysisData.heroLevels.map((level) => `Lv.${level}`),
      datasets: [
        {
          data: analysisData.winProbabilities,
          label: '勝利機率',
          fill: true,
          tension: 0.4,
          borderColor: '#667eea',
          backgroundColor: 'rgba(102, 126, 234, 0.1)',
          pointBackgroundColor: '#667eea',
          pointBorderColor: '#fff',
          pointRadius: 4,
          pointHoverRadius: 6,
        },
      ],
    };
  });

  readonly chartOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    resizeDelay: 100,
    plugins: {
      legend: {
        display: true,
        position: 'top',
        labels: {
          color: '#6b7280',
          font: {
            size: 12,
            family: "'Segoe UI', 'PingFang SC', 'Microsoft YaHei', system-ui, -apple-system, sans-serif",
          },
          usePointStyle: true,
          padding: 16,
        },
      },
      tooltip: {
        enabled: true,
        backgroundColor: 'rgba(31, 41, 55, 0.95)',
        titleFont: {
          size: 13,
          family: "'Segoe UI', 'PingFang SC', 'Microsoft YaHei', system-ui, -apple-system, sans-serif",
        },
        bodyFont: {
          size: 12,
          family: "'Segoe UI', 'PingFang SC', 'Microsoft YaHei', system-ui, -apple-system, sans-serif",
        },
        padding: 12,
        cornerRadius: 8,
        callbacks: {
          label: (context) => {
            const value = context.parsed.y ?? 0;
            return `勝利機率: ${(value * 100).toFixed(1)}%`;
          },
        },
      },
    },
    scales: {
      x: {
        grid: { color: 'rgba(0, 0, 0, 0.05)' },
        ticks: {
          color: '#6b7280',
          font: {
            size: 11,
            family: "'Segoe UI', 'PingFang SC', 'Microsoft YaHei', system-ui, -apple-system, sans-serif",
          },
          maxRotation: 0,
          autoSkip: true,
          maxTicksLimit: 15,
        },
      },
      y: {
        min: 0,
        max: 1,
        grid: { color: 'rgba(0, 0, 0, 0.05)' },
        ticks: {
          color: '#6b7280',
          font: {
            size: 11,
            family: "'Segoe UI', 'PingFang SC', 'Microsoft YaHei', system-ui, -apple-system, sans-serif",
          },
          callback: (value) => `${(Number(value) * 100).toFixed(0)}%`,
          maxRotation: 0,
        },
      },
    },
    interaction: {
      intersect: false,
      mode: 'index',
    },
    animation: {
      duration: 400,
      easing: 'easeOutQuart',
    },
  };

  // ── Computed ────────────────────────────────────────────────────
  readonly hasData = computed(() => {
    const d = this.data();
    return d && d.heroLevels.length > 0;
  });

  readonly stats = computed(() => {
    const d = this.data();
    if (!d || !d.winProbabilities.length) {
      return { maxWinRate: 0, bestLevel: 0, avgWinRate: 0 };
    }

    const maxIndex = d.winProbabilities.indexOf(Math.max(...d.winProbabilities));
    const avg = d.winProbabilities.reduce((a, b) => a + b, 0) / d.winProbabilities.length;

    return {
      maxWinRate: d.winProbabilities[maxIndex],
      bestLevel: d.heroLevels[maxIndex],
      avgWinRate: avg,
    };
  });

  // ── Lifecycle ──────────────────────────────────────────────────
  ngOnInit(): void {
    this.initForm();
  }

  // ── Public Methods ─────────────────────────────────────────────

  /**
   * Trigger analysis with current form parameters.
   */
  onAnalyze(): void {
    const value = this.form.getRawValue() as {
      heroLevel?: number;
      heroKills?: number;
      deaths?: number;
      totalGold?: number;
      unitKills?: number;
      highestAtk?: number;
      highestDef?: number;
      highestSpeed?: number;
      playerCount?: number;
    };
    this.analyze.emit({
      heroLevel: value.heroLevel,
      heroKills: value.heroKills,
      deaths: value.deaths,
      totalGold: value.totalGold,
      unitKills: value.unitKills,
      highestAtk: value.highestAtk,
      highestDef: value.highestDef,
      highestSpeed: value.highestSpeed,
      playerCount: value.playerCount,
    });
  }

  // ── Private Methods ────────────────────────────────────────────

  private initForm(): void {
    const defaults = DEFAULT_PREDICTION_INPUT;
    this.form = this.fb.group({
      heroLevel: [defaults.heroLevel],
      heroKills: [defaults.heroKills],
      deaths: [defaults.deaths],
      totalGold: [defaults.totalGold],
      unitKills: [defaults.unitKills],
      highestAtk: [defaults.highestAtk],
      highestDef: [defaults.highestDef],
      highestSpeed: [defaults.highestSpeed],
      playerCount: [defaults.playerCount],
    });
  }
}
