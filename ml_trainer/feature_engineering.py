"""
Feature Engineering Module

Transforms raw CSV features into derived features for ML training.
This implementation must match the .NET FeatureEngineeringService.
"""

import pandas as pd
import numpy as np
from typing import List, Optional


class FeatureEngineering:
    """
    Feature engineering for game state prediction.

    Transforms raw features from CSV into derived features:
    - Ratio features (gold_per_level, kd_ratio, etc.)
    - Aggregate features (total_kills)
    - Efficiency metrics (gold_efficiency)

    IMPORTANT: This implementation must match the .NET
    FeatureEngineeringService to ensure consistent feature
    transformation during online prediction.
    """

    # Feature columns that will be used in the model
    RAW_FEATURES = [
        'player_count', 'hour_of_day', 'day_of_week',
        'hero_level', 'hero_kills', 'deaths', 'unit_kills',
        'total_gold', 'highest_atk', 'highest_def', 'highest_speed', 'atk_range'
    ]

    DERIVED_FEATURES = [
        'gold_per_level', 'atk_per_level', 'def_per_level', 'speed_per_level',
        'kd_ratio', 'total_kills', 'gold_efficiency', 'death_ratio'
    ]

    ALL_FEATURES = RAW_FEATURES + DERIVED_FEATURES

    def __init__(self):
        self.feature_columns_: List[str] = []
        self._fitted = False

    def fit_transform(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Fit the feature engineering and transform the data.

        Args:
            df: DataFrame with raw features

        Returns:
            DataFrame with both raw and derived features
        """
        df = df.copy()

        # Validate required columns
        missing = set(self.RAW_FEATURES) - set(df.columns)
        if missing:
            raise ValueError(f"Missing required columns: {missing}")

        # Create derived features
        df = self._create_derived_features(df)

        # Handle invalid values
        df = self._clean_data(df)

        # Record feature columns
        self.feature_columns_ = self.ALL_FEATURES.copy()
        self._fitted = True

        return df

    def transform(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Transform data using fitted feature engineering.

        Args:
            df: DataFrame with raw features

        Returns:
            DataFrame with both raw and derived features
        """
        if not self._fitted:
            raise RuntimeError("FeatureEngineering has not been fitted. Call fit_transform first.")
        return self.fit_transform(df)

    def fit(self, df: pd.DataFrame) -> 'FeatureEngineering':
        """
        Fit the feature engineering (alias for fit_transform).

        Args:
            df: DataFrame with raw features

        Returns:
            self
        """
        self.fit_transform(df)
        return self

    def _create_derived_features(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Create derived features from raw features.

        Note: We add 1 to denominator to avoid division by zero.
        """
        # Per-level metrics
        df['gold_per_level'] = df['total_gold'] / (df['hero_level'] + 1)
        df['atk_per_level'] = df['highest_atk'] / (df['hero_level'] + 1)
        df['def_per_level'] = df['highest_def'] / (df['hero_level'] + 1)
        df['speed_per_level'] = df['highest_speed'] / (df['hero_level'] + 1)

        # KDA metrics
        df['kd_ratio'] = df['hero_kills'] / (df['deaths'] + 1)
        df['total_kills'] = df['hero_kills'] + df['unit_kills']

        # Efficiency metrics
        df['gold_efficiency'] = df['total_gold'] / (df['hero_kills'] + 1)
        df['death_ratio'] = df['deaths'] / (df['player_count'] + 1)

        return df

    def _clean_data(self, df: pd.DataFrame) -> pd.DataFrame:
        """
        Clean invalid values (NaN, Inf) from the dataframe.
        """
        # Fill NaN with 0
        df = df.fillna(0)

        # Replace infinity with 0
        df = df.replace([np.inf, -np.inf], 0)

        return df

    def get_feature_columns(self) -> List[str]:
        """
        Get the list of feature columns after transformation.

        Returns:
            List of feature column names
        """
        if not self._fitted:
            raise RuntimeError("FeatureEngineering has not been fitted.")
        return self.feature_columns_.copy()

    def get_raw_feature_columns(self) -> List[str]:
        """Get raw feature columns (before engineering)."""
        return self.RAW_FEATURES.copy()

    def get_derived_feature_columns(self) -> List[str]:
        """Get derived feature columns (after engineering)."""
        return self.DERIVED_FEATURES.copy()


def validate_input(df: pd.DataFrame, min_samples: int = 10) -> None:
    """
    Validate input DataFrame for ML training.

    Args:
        df: Input DataFrame
        min_samples: Minimum number of samples required

    Raises:
        ValueError: If validation fails
    """
    if df.empty:
        raise ValueError("DataFrame is empty")

    if len(df) < min_samples:
        raise ValueError(f"Insufficient samples: {len(df)} < {min_samples}")

    # Check required columns
    required = ['is_winner'] + FeatureEngineering.RAW_FEATURES
    missing = set(required) - set(df.columns)
    if missing:
        raise ValueError(f"Missing required columns: {missing}")

    # Check label distribution
    label_counts = df['is_winner'].value_counts()
    if len(label_counts) < 2:
        raise ValueError("Only one class present in labels (is_winner)")

    # Warn if class imbalance is severe
    min_class_ratio = min(label_counts) / len(df)
    if min_class_ratio < 0.1:
        import warnings
        warnings.warn(
            f"[WARNING] Severe class imbalance: "
            f"minority class is {min_class_ratio:.1%} of samples. "
            f"Consider using stratified sampling or class weights."
        )


def get_sample_data() -> pd.DataFrame:
    """
    Generate sample data for testing the feature engineering.

    Returns:
        DataFrame with sample game data
    """
    np.random.seed(42)

    n_samples = 100
    data = {
        'player_count': np.random.choice([3, 4, 5, 6], n_samples),
        'hour_of_day': np.random.randint(0, 24, n_samples),
        'day_of_week': np.random.randint(0, 7, n_samples),
        'hero_level': np.random.randint(5, 20, n_samples),
        'hero_kills': np.random.randint(0, 15, n_samples),
        'deaths': np.random.randint(0, 10, n_samples),
        'unit_kills': np.random.randint(5, 50, n_samples),
        'total_gold': np.random.randint(2000, 8000, n_samples),
        'highest_atk': np.random.randint(50, 200, n_samples),
        'highest_def': np.random.randint(30, 150, n_samples),
        'highest_speed': np.random.randint(250, 450, n_samples),
        'atk_range': np.random.choice([100, 150, 200, 250], n_samples),
        'is_winner': np.random.choice([0, 1], n_samples, p=[0.45, 0.55])
    }

    return pd.DataFrame(data)


if __name__ == '__main__':
    # Test feature engineering
    print("Testing FeatureEngineering...")

    # Generate sample data
    df = get_sample_data()
    print(f"Generated {len(df)} sample records")

    # Apply feature engineering
    fe = FeatureEngineering()
    df_transformed = fe.fit_transform(df)

    print(f"\nFeature columns ({len(fe.get_feature_columns())}):")
    for col in fe.get_feature_columns():
        print(f"  - {col}")

    print(f"\nDerived features sample:")
    for col in fe.get_derived_feature_columns():
        print(f"  {col}: mean={df_transformed[col].mean():.2f}, std={df_transformed[col].std():.2f}")

    print("\nValidation test:")
    validate_input(df)
    print("  ✓ All validations passed")
