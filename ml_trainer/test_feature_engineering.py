#!/usr/bin/env python3
"""
Test Feature Engineering Module

Run this to verify the feature engineering code works correctly.
"""

import sys
import os

# Add parent directory to path
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

from feature_engineering import FeatureEngineering, validate_input, get_sample_data


def test_feature_engineering():
    """Test the feature engineering module."""
    print("=" * 60)
    print("Testing Feature Engineering Module")
    print("=" * 60)

    # Generate sample data
    print("\n1. Generating sample data...")
    df = get_sample_data()
    print(f"   Generated {len(df)} sample records")
    print(f"   Columns: {list(df.columns)}")

    # Test validation
    print("\n2. Testing validation...")
    try:
        validate_input(df)
        print("   [OK] Validation passed")
    except ValueError as e:
        print(f"   [FAIL] Validation failed: {e}")
        return False

    # Test feature engineering
    print("\n3. Testing feature engineering...")
    fe = FeatureEngineering()
    df_transformed = fe.fit_transform(df)

    print(f"   Original columns: {len(df.columns)}")
    print(f"   Transformed columns: {len(df_transformed.columns)}")
    print(f"   Feature columns: {fe.get_feature_columns()}")

    # Check derived features
    print("\n4. Checking derived features...")
    for col in fe.get_derived_feature_columns():
        if col in df_transformed.columns:
            mean_val = df_transformed[col].mean()
            print(f"   {col}: mean={mean_val:.2f}, std={df_transformed[col].std():.2f}")
        else:
            print(f"   [FAIL] Missing derived feature: {col}")
            return False

    # Verify calculation correctness
    print("\n5. Verifying calculation correctness...")
    test_cases = [
        ('gold_per_level', lambda df: df['total_gold'] / (df['hero_level'] + 1)),
        ('kd_ratio', lambda df: df['hero_kills'] / (df['deaths'] + 1)),
        ('total_kills', lambda df: df['hero_kills'] + df['unit_kills']),
    ]

    for col, expected_fn in test_cases:
        expected = expected_fn(df)
        actual = df_transformed[col].values
        if not all(abs(expected - actual) < 0.001):
            print(f"   [FAIL] {col} calculation mismatch")
            return False
        print(f"   [OK] {col} calculation correct")

    print("\n" + "=" * 60)
    print("All tests passed!")
    print("=" * 60)
    return True


if __name__ == '__main__':
    success = test_feature_engineering()
    sys.exit(0 if success else 1)
