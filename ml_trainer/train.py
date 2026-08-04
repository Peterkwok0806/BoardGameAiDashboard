#!/usr/bin/env python3
"""
ML Training Script: Random Forest Training + ONNX Export

Responsibilities:
- .NET exports CSV (raw data only)
- Python handles feature engineering, training, and ONNX export

Usage:
    python train.py --input training_data.csv --output ./models
    python train.py --input training_data.csv --output ./models --folds 5
    python train.py --input training_data.csv --output ./models --n-estimators 200 --max-depth 15
"""

import argparse
import json
import os
import sys
import warnings
from datetime import datetime
from pathlib import Path
from typing import Optional

import pandas as pd
import numpy as np
from sklearn.model_selection import StratifiedKFold
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score,
    f1_score, log_loss, roc_auc_score, confusion_matrix
)

from feature_engineering import FeatureEngineering, validate_input
from models.random_forest_model import RandomForestModel, ModelMetrics


class TrainingResult:
    """Training result container."""

    def __init__(self):
        self.model_name = "RandomForest"
        self.metrics: Optional[ModelMetrics] = None
        self.feature_columns: list = []
        self.timestamp = datetime.utcnow().isoformat()
        self.model_params: dict = {}

    def to_dict(self) -> dict:
        return {
            'model_name': self.model_name,
            'metrics': self.metrics.to_dict() if self.metrics else {},
            'feature_columns': self.feature_columns,
            'timestamp': self.timestamp,
            'model_params': self.model_params
        }


def load_data(csv_path: str) -> pd.DataFrame:
    """
    Load training data from CSV file.

    Args:
        csv_path: Path to the CSV file

    Returns:
        Loaded DataFrame

    Raises:
        FileNotFoundError: If CSV file doesn't exist
        ValueError: If required columns are missing
    """
    print(f"Loading data: {csv_path}")

    if not os.path.exists(csv_path):
        raise FileNotFoundError(f"CSV file not found: {csv_path}")

    df = pd.read_csv(csv_path)
    print(f"Loaded {len(df)} records, {len(df.columns)} columns")

    # Check required columns
    required = ['is_winner'] + FeatureEngineering.RAW_FEATURES
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    # Show label distribution
    label_counts = df['is_winner'].value_counts()
    print(f"Label distribution: Win={label_counts.get(1, 0)}, Loss={label_counts.get(0, 0)}")
    print(f"Win rate: {df['is_winner'].mean():.2%}")

    return df


def cross_validate(
    model: RandomForestModel,
    X: pd.DataFrame,
    y: pd.Series,
    folds: int = 5
) -> tuple[ModelMetrics, list[dict]]:
    """
    Perform stratified k-fold cross-validation.

    Args:
        model: Model to train
        X: Feature matrix
        y: Labels
        folds: Number of folds

    Returns:
        Tuple of (average metrics, per-fold metrics)
    """
    print(f"\nRunning {folds}-Fold Stratified Cross-Validation...")
    print("-" * 60)

    skf = StratifiedKFold(n_splits=folds, shuffle=True, random_state=42)
    fold_metrics: list = []
    all_y_true: list = []
    all_y_pred: list = []
    all_y_prob: list = []

    for fold, (train_idx, val_idx) in enumerate(skf.split(X, y), 1):
        X_train, X_val = X.iloc[train_idx], X.iloc[val_idx]
        y_train, y_val = y.iloc[train_idx], y.iloc[val_idx]

        # Clone model for this fold
        fold_model = RandomForestModel(
            n_estimators=model.n_estimators,
            max_depth=model.max_depth,
            min_samples_leaf=model.min_samples_leaf,
            random_state=model.random_state
        )

        # Train
        fold_model.fit(X_train, y_train)

        # Predict
        y_pred = fold_model.predict(X_val)
        y_prob = fold_model.predict_proba(X_val)[:, 1]

        # Store predictions for aggregate metrics
        all_y_true.extend(y_val)
        all_y_pred.extend(y_pred)
        all_y_prob.extend(y_prob)

        # Calculate fold metrics
        metrics = ModelMetrics(
            accuracy=accuracy_score(y_val, y_pred),
            precision=precision_score(y_val, y_pred, zero_division=0),
            recall=recall_score(y_val, y_pred, zero_division=0),
            f1=f1_score(y_val, y_pred, zero_division=0),
            log_loss=log_loss(y_val, y_prob),
            auc=roc_auc_score(y_val, y_prob)
        )

        fold_metrics.append({
            'fold': fold,
            'accuracy': metrics.accuracy,
            'precision': metrics.precision,
            'recall': metrics.recall,
            'f1': metrics.f1,
            'log_loss': metrics.log_loss,
            'auc': metrics.auc
        })

        print(f"Fold {fold}: Acc={metrics.accuracy:.4f}, AUC={metrics.auc:.4f}, F1={metrics.f1:.4f}")

    # Calculate average metrics
    avg_metrics = ModelMetrics(
        accuracy=np.mean([m['accuracy'] for m in fold_metrics]),
        precision=np.mean([m['precision'] for m in fold_metrics]),
        recall=np.mean([m['recall'] for m in fold_metrics]),
        f1=np.mean([m['f1'] for m in fold_metrics]),
        log_loss=np.mean([m['log_loss'] for m in fold_metrics]),
        auc=np.mean([m['auc'] for m in fold_metrics])
    )

    print("-" * 60)
    print(f"Average: Acc={avg_metrics.accuracy:.4f}, AUC={avg_metrics.auc:.4f}, F1={avg_metrics.f1:.4f}")

    return avg_metrics, fold_metrics


def print_confusion_matrix(y_true, y_pred):
    """Print confusion matrix in a readable format."""
    cm = confusion_matrix(y_true, y_pred)
    print("\nConfusion Matrix:")
    print(f"                Predicted")
    print(f"              Loss    Win")
    print(f"Actual Loss   {cm[0,0]:4d}   {cm[0,1]:4d}")
    print(f"Actual Win   {cm[1,0]:4d}   {cm[1,1]:4d}")


def print_feature_importance(model: RandomForestModel, top_n: int = 10):
    """Print feature importance in a readable format."""
    print(f"\nTop {top_n} Feature Importance:")
    print("-" * 40)
    print(f"{'Feature':<20} {'Importance':>10}")
    print("-" * 40)

    for name, importance in model.get_top_features(top_n):
        bar = "█" * int(importance * 50)
        print(f"{name:<20} {importance:>10.4f} {bar}")


def main():
    parser = argparse.ArgumentParser(
        description='ML Training Script (Random Forest)',
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python train.py --input training_data.csv --output ./models
  python train.py --input training_data.csv --output ./models --folds 10
  python train.py --input training_data.csv --output ./models -n 200 -d 15
        """
    )

    parser.add_argument(
        '--input', '-i',
        required=True,
        help='Input CSV file path'
    )
    parser.add_argument(
        '--output', '-o',
        default='./models',
        help='Output directory for models (default: ./models)'
    )
    parser.add_argument(
        '--folds', '-k',
        type=int,
        default=5,
        help='Number of cross-validation folds (default: 5)'
    )
    parser.add_argument(
        '--min-samples', '-m',
        type=int,
        default=20,
        help='Minimum training samples required (default: 20)'
    )
    parser.add_argument(
        '--n-estimators', '-n',
        type=int,
        default=100,
        help='Number of trees in the forest (default: 100)'
    )
    parser.add_argument(
        '--max-depth', '-d',
        type=int,
        default=10,
        help='Maximum tree depth (default: 10)'
    )
    parser.add_argument(
        '--min-samples-leaf',
        type=int,
        default=5,
        help='Minimum samples per leaf node (default: 5)'
    )
    parser.add_argument(
        '--random-seed',
        type=int,
        default=42,
        help='Random seed for reproducibility (default: 42)'
    )
    parser.add_argument(
        '--no-validate',
        action='store_true',
        help='Skip input validation'
    )
    parser.add_argument(
        '--verbose', '-v',
        action='store_true',
        help='Verbose output'
    )

    args = parser.parse_args()

    # =========================================================================
    # Step 1: Load Data
    # =========================================================================
    print("\n" + "=" * 60)
    print("ML Training Pipeline - Random Forest")
    print("=" * 60)

    try:
        df = load_data(args.input)
    except (FileNotFoundError, ValueError) as e:
        print(f"\nError: {e}")
        sys.exit(1)

    # Validate data
    if not args.no_validate:
        print("\nValidating input data...")
        try:
            validate_input(df, args.min_samples)
            print("✓ Validation passed")
        except ValueError as e:
            print(f"\nError: {e}")
            sys.exit(1)

    # =========================================================================
    # Step 2: Feature Engineering
    # =========================================================================
    print("\n" + "-" * 60)
    print("Feature Engineering")
    print("-" * 60)

    fe = FeatureEngineering()
    df_transformed = fe.fit_transform(df)

    # Get features and labels
    feature_columns = fe.get_feature_columns()
    X = df_transformed[feature_columns]
    y = df_transformed['is_winner']

    print(f"Raw features: {len(fe.get_raw_feature_columns())}")
    print(f"Derived features: {len(fe.get_derived_feature_columns())}")
    print(f"Total features: {len(feature_columns)}")
    print(f"\nFeature columns:")
    for i, col in enumerate(feature_columns, 1):
        print(f"  {i:2d}. {col}")

    # =========================================================================
    # Step 3: Cross-Validation
    # =========================================================================
    print("\n" + "=" * 60)
    print(f"Cross-Validation ({args.folds}-Fold)")
    print("=" * 60)

    # Create model with specified parameters
    model = RandomForestModel(
        n_estimators=args.n_estimators,
        max_depth=args.max_depth,
        min_samples_leaf=args.min_samples_leaf,
        random_state=args.random_seed
    )

    # Store model params for report
    model_params = {
        'n_estimators': args.n_estimators,
        'max_depth': args.max_depth,
        'min_samples_leaf': args.min_samples_leaf,
        'random_state': args.random_seed,
        'cv_folds': args.folds
    }

    # Run cross-validation
    avg_metrics, fold_metrics = cross_validate(model, X, y, args.folds)

    # =========================================================================
    # Step 4: Train Final Model
    # =========================================================================
    print("\n" + "=" * 60)
    print("Training Final Model (on all data)")
    print("=" * 60)

    model.fit(X, y)
    print(f"✓ Final model trained on {len(X)} samples")

    # =========================================================================
    # Step 5: Export Model
    # =========================================================================
    print("\n" + "=" * 60)
    print("Exporting Model")
    print("=" * 60)

    output_dir = Path(args.output)
    output_dir.mkdir(parents=True, exist_ok=True)

    timestamp = datetime.utcnow().strftime('%Y%m%d%H%M%S')
    model_filename = f'winrate_model_{timestamp}.onnx'
    model_path = output_dir / model_filename

    # Export ONNX
    print(f"Exporting ONNX model: {model_path}")
    model.export_onnx(str(model_path), feature_columns)

    # =========================================================================
    # Step 6: Save Reports
    # =========================================================================
    print("\n" + "=" * 60)
    print("Saving Reports")
    print("=" * 60)

    # Training report
    result = TrainingResult()
    result.metrics = avg_metrics
    result.feature_columns = feature_columns
    result.model_params = model_params

    report_path = output_dir / f'training_report_{timestamp}.json'
    with open(report_path, 'w', encoding='utf-8') as f:
        json.dump(result.to_dict(), f, indent=2, ensure_ascii=False)
    print(f"✓ Training report: {report_path}")

    # Feature importance
    importance_path = output_dir / f'feature_importance_{timestamp}.json'
    importance = model.get_feature_importance()
    with open(importance_path, 'w', encoding='utf-8') as f:
        json.dump(importance, f, indent=2)
    print(f"✓ Feature importance: {importance_path}")

    # Cross-validation details
    cv_path = output_dir / f'cv_results_{timestamp}.json'
    with open(cv_path, 'w', encoding='utf-8') as f:
        json.dump({
            'folds': args.folds,
            'per_fold': fold_metrics,
            'average': avg_metrics.to_dict()
        }, f, indent=2)
    print(f"✓ CV results: {cv_path}")

    # =========================================================================
    # Summary
    # =========================================================================
    print("\n" + "=" * 60)
    print("Training Complete!")
    print("=" * 60)
    print(f"Model: RandomForest")
    print(f"Parameters: {model_params}")
    print(f"\nCross-Validation Results:")
    print(f"  Accuracy:  {avg_metrics.accuracy:.4f}")
    print(f"  Precision: {avg_metrics.precision:.4f}")
    print(f"  Recall:    {avg_metrics.recall:.4f}")
    print(f"  F1 Score:  {avg_metrics.f1:.4f}")
    print(f"  AUC:       {avg_metrics.auc:.4f}")
    print(f"\nOutput Files:")
    print(f"  Model: {model_path}")
    print(f"  Report: {report_path}")
    print(f"  Features: {model_path.with_suffix('.onnx_features.json')}")
    print("=" * 60)

    # Print additional info
    if args.verbose:
        print("\nFeature Importance (Top 10):")
        print_feature_importance(model, 10)


if __name__ == '__main__':
    main()
