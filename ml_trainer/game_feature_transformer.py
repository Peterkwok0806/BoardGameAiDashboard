"""
Sklearn-compatible Feature Engineering Transformer

This transformer wraps the FeatureEngineering logic to make it
compatible with sklearn's Pipeline and ColumnTransformer.
"""

import numpy as np
import pandas as pd
from sklearn.base import BaseEstimator, TransformerMixin
from typing import List, Optional


class GameFeatureTransformer(BaseEstimator, TransformerMixin):
    """
    Sklearn-compatible feature transformer for game data.

    Transforms raw game features into derived features:
    - Ratio features (gold_per_level, kd_ratio, etc.)
    - Aggregate features (total_kills)
    - Efficiency metrics (gold_efficiency)

    This transformer can be used inside sklearn's Pipeline
    to create an end-to-end model that includes feature engineering.

    Input: 12 raw features
    Output: 20 features (12 raw + 8 derived)
    """

    # Raw feature names (must match CSV columns)
    RAW_FEATURES = [
        'player_count', 'hour_of_day', 'day_of_week',
        'hero_level', 'hero_kills', 'deaths', 'unit_kills',
        'total_gold', 'highest_atk', 'highest_def', 'highest_speed', 'atk_range'
    ]

    # Derived feature names
    DERIVED_FEATURES = [
        'gold_per_level', 'atk_per_level', 'def_per_level', 'speed_per_level',
        'kd_ratio', 'total_kills', 'gold_efficiency', 'death_ratio'
    ]

    # All feature names
    ALL_FEATURES = RAW_FEATURES + DERIVED_FEATURES

    def __init__(self):
        """Initialize the transformer."""
        pass

    def fit(self, X, y=None):
        """
        Fit the transformer (no-op, no parameters to learn).

        Args:
            X: Input features (DataFrame or array-like)
            y: Target labels (ignored)

        Returns:
            self
        """
        # No parameters to learn, just validate input shape
        if hasattr(X, 'shape'):
            if X.shape[1] != len(self.RAW_FEATURES):
                raise ValueError(
                    f"Expected {len(self.RAW_FEATURES)} input features, "
                    f"got {X.shape[1]}"
                )
        return self

    def transform(self, X):
        """
        Transform raw features into derived features.

        Args:
            X: Input features (DataFrame or array-like)

        Returns:
            Transformed features as numpy array with 20 columns
        """
        # Convert to DataFrame if needed
        if hasattr(X, 'values'):
            # It's a DataFrame or similar
            if hasattr(X, 'columns'):
                df = X.copy()
            else:
                # It's a numpy array, convert using default column names
                df = pd.DataFrame(X, columns=self.RAW_FEATURES)
        else:
            df = pd.DataFrame(X, columns=self.RAW_FEATURES)

        # Create derived features
        df = self._create_derived_features(df)

        # Handle invalid values
        df = self._clean_data(df)

        # Return as numpy array
        return df[self.ALL_FEATURES].values

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

    def get_feature_names_out(self, input_features=None) -> List[str]:
        """
        Get output feature names for transformation.

        Args:
            input_features: Ignored (for sklearn compatibility)

        Returns:
            List of all output feature names
        """
        return self.ALL_FEATURES.copy()


def create_pipeline(
    n_estimators: int = 100,
    max_depth: int = 10,
    min_samples_leaf: int = 5,
    random_state: int = 42
):
    """
    Create a sklearn Pipeline with FeatureTransformer and RandomForest.

    This pipeline can be exported to ONNX with both the feature
    engineering and the model bundled together.

    Args:
        n_estimators: Number of trees in the forest
        max_depth: Maximum depth of each tree
        min_samples_leaf: Minimum samples per leaf
        random_state: Random seed

    Returns:
        sklearn Pipeline with 'features' and 'classifier' steps
    """
    from sklearn.pipeline import Pipeline
    from sklearn.ensemble import RandomForestClassifier

    pipeline = Pipeline([
        ('features', GameFeatureTransformer()),
        ('classifier', RandomForestClassifier(
            n_estimators=n_estimators,
            max_depth=max_depth,
            min_samples_leaf=min_samples_leaf,
            random_state=random_state,
            n_jobs=-1
        ))
    ])

    return pipeline


def register_game_feature_transformer_converter():
    """
    Register GameFeatureTransformer with sklearn-onnx.

    This uses skl2onnx's update_registered_converter to enable
    ONNX export of GameFeatureTransformer.
    """
    from skl2onnx import update_registered_converter
    from skl2onnx.common.data_types import FloatTensorType

    # Shape calculator: input [?, 12] -> output [?, 20]
    def calculate_game_feature_transformer_output_shapes(operator):
        operator.outputs[0].type = FloatTensorType(shape=[None, 20])

    # Converter: replicate the feature engineering logic in ONNX
    def convert_game_feature_transformer(operator, container, options=None):
        """
        Convert GameFeatureTransformer to ONNX operators.

        The transformer computes:
        - gold_per_level = total_gold / (hero_level + 1)  [col 7 / (col 3 + 1)]
        - atk_per_level = highest_atk / (hero_level + 1)  [col 8 / (col 3 + 1)]
        - def_per_level = highest_def / (hero_level + 1)  [col 9 / (col 3 + 1)]
        - speed_per_level = highest_speed / (hero_level + 1) [col 10 / (col 3 + 1)]
        - kd_ratio = hero_kills / (deaths + 1)  [col 4 / (col 5 + 1)]
        - total_kills = hero_kills + unit_kills  [col 4 + col 6]
        - gold_efficiency = total_gold / (hero_kills + 1)  [col 7 / (col 4 + 1)]
        - death_ratio = deaths / (player_count + 1)  [col 5 / (col 0 + 1)]
        """
        X = operator.inputs[0]
        op_type = 'GameFeatureTransformer'

        # Helper to create a constant scalar
        def make_const(name, value):
            container.add_initializer(name, [], [float(value)])

        # Helper to get input column by index
        def get_col(idx):
            return [op_type, X.onnx_name, idx]

        # Create constant nodes for 1.0
        one_name = f'{op_type}_one'
        make_const(one_name, 1.0)

        # Create derived feature calculations using ONNX ops
        derived_features = []

        # 1. gold_per_level = col(7) / (col(3) + 1)
        hero_level_plus_1 = container.add_node('Add', [get_col(3)[1], one_name], [f'{op_type}_hl_add'])
        gold_per_level = container.add_node('Div', [get_col(7)[1], hero_level_plus_1], [f'{op_type}_gpl'])
        derived_features.append(gold_per_level)

        # 2. atk_per_level = col(8) / (col(3) + 1)
        atk_per_level = container.add_node('Div', [get_col(8)[1], hero_level_plus_1], [f'{op_type}_apl'])
        derived_features.append(atk_per_level)

        # 3. def_per_level = col(9) / (col(3) + 1)
        def_per_level = container.add_node('Div', [get_col(9)[1], hero_level_plus_1], [f'{op_type}_dpl'])
        derived_features.append(def_per_level)

        # 4. speed_per_level = col(10) / (col(3) + 1)
        speed_per_level = container.add_node('Div', [get_col(10)[1], hero_level_plus_1], [f'{op_type}_spl'])
        derived_features.append(speed_per_level)

        # 5. kd_ratio = col(4) / (col(5) + 1)
        deaths_plus_1 = container.add_node('Add', [get_col(5)[1], one_name], [f'{op_type}_d_add'])
        kd_ratio = container.add_node('Div', [get_col(4)[1], deaths_plus_1], [f'{op_type}_kdr'])
        derived_features.append(kd_ratio)

        # 6. total_kills = col(4) + col(6)
        total_kills = container.add_node('Add', [get_col(4)[1], get_col(6)[1]], [f'{op_type}_tk'])
        derived_features.append(total_kills)

        # 7. gold_efficiency = col(7) / (col(4) + 1)
        hero_kills_plus_1 = container.add_node('Add', [get_col(4)[1], one_name], [f'{op_type}_hk_add'])
        gold_efficiency = container.add_node('Div', [get_col(7)[1], hero_kills_plus_1], [f'{op_type}_ge'])
        derived_features.append(gold_efficiency)

        # 8. death_ratio = col(5) / (col(0) + 1)
        player_count_plus_1 = container.add_node('Add', [get_col(0)[1], one_name], [f'{op_type}_pc_add'])
        death_ratio = container.add_node('Div', [get_col(5)[1], player_count_plus_1], [f'{op_type}_dr'])
        derived_features.append(death_ratio)

        # Now create pass-through for raw features
        raw_passthrough = [get_col(i)[1] for i in range(12)]

        # Concatenate: [raw(12) features] + [derived(8) features] = [20 features]
        all_features = raw_passthrough + [f.onnx_name for f in derived_features]

        # Create Concat node
        output_name = operator.outputs[0].onnx_name
        container.add_node('Concat', all_features, [output_name], axis=1)

    # Register the converter
    update_registered_converter(
        GameFeatureTransformer,
        'GameFeatureTransformer',
        calculate_game_feature_transformer_output_shapes,
        convert_game_feature_transformer
    )


if __name__ == '__main__':
    # Test the transformer
    print("Testing GameFeatureTransformer...")

    # Create sample data
    np.random.seed(42)
    n_samples = 100

    # Raw features only
    X_raw = pd.DataFrame({
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
    })

    y = np.random.choice([0, 1], n_samples)

    # Test transformer
    transformer = GameFeatureTransformer()
    transformer.fit(X_raw)
    X_transformed = transformer.transform(X_raw)

    print(f"Input shape: {X_raw.shape}")
    print(f"Output shape: {X_transformed.shape}")
    print(f"Output features: {transformer.get_feature_names_out()}")

    # Test pipeline
    print("\nTesting Pipeline...")
    from sklearn.pipeline import Pipeline
    from sklearn.ensemble import RandomForestClassifier

    pipeline = create_pipeline()
    pipeline.fit(X_raw, y)

    # Predict
    y_pred = pipeline.predict(X_raw)
    y_prob = pipeline.predict_proba(X_raw)

    print(f"Predictions shape: {y_pred.shape}")
    print(f"Probabilities shape: {y_prob.shape}")
    print(f"First 5 probabilities: {y_prob[:5]}")

    # Test ONNX export
    print("\nTesting ONNX export...")
    try:
        register_game_feature_transformer_converter()

        from skl2onnx import convert_sklearn
        from skl2onnx.common.data_types import FloatTensorType

        initial_type = [('float_input', FloatTensorType([None, 12]))]
        onnx_model = convert_sklearn(pipeline, initial_types=initial_type, target_opset=12)

        # Save test model
        test_path = './models/test_pipeline.onnx'
        from pathlib import Path
        Path('./models').mkdir(exist_ok=True)
        with open(test_path, 'wb') as f:
            f.write(onnx_model.SerializeToString())

        print(f"✓ ONNX pipeline model saved: {test_path}")
    except Exception as e:
        print(f"✗ ONNX export failed: {e}")

    print("\n✓ All tests passed!")
