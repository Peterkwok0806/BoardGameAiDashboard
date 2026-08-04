"""
RandomForest Classification Model

Uses scikit-learn's RandomForestClassifier and exports to ONNX format
for use with .NET OnnxRuntime.

Architecture:
    - Training: scikit-learn RandomForestClassifier
    - Export: skl2onnx for ONNX format
    - Inference: Microsoft.ML.OnnxRuntime (in .NET)
"""

import json
import joblib
from pathlib import Path
from typing import List, Dict, Optional, Tuple
from dataclasses import dataclass

from sklearn.ensemble import RandomForestClassifier
from sklearn.metrics import (
    accuracy_score, precision_score, recall_score,
    f1_score, log_loss, roc_auc_score
)
from skl2onnx.common.data_types import FloatTensorType
from skl2onnx import convert_sklearn


@dataclass
class ModelMetrics:
    """Container for model evaluation metrics."""
    accuracy: float
    precision: float
    recall: float
    f1: float
    log_loss: float
    auc: float

    def to_dict(self) -> Dict:
        return {
            'accuracy': self.accuracy,
            'precision': self.precision,
            'recall': self.recall,
            'f1': self.f1,
            'log_loss': self.log_loss,
            'auc': self.auc
        }


class RandomForestModel:
    """
    RandomForest classifier for win rate prediction.

    This model is trained using scikit-learn and exported to ONNX format
    for use in the .NET backend with OnnxRuntime.

    Attributes:
        n_estimators: Number of trees in the forest
        max_depth: Maximum depth of the tree
        min_samples_leaf: Minimum number of samples required at a leaf node
        model: The underlying sklearn RandomForestClassifier
        feature_columns_: List of feature column names
    """

    MODEL_NAME = "RandomForest"
    ONNX_INPUT_NAME = "float_input"
    ONNX_OUTPUT_LABEL = "output_label"
    ONNX_OUTPUT_PROBABILITY = "output_probability"
    TARGET_OPSET = 12  # ONNX Runtime 1.16+ supports opSet 12

    def __init__(
        self,
        n_estimators: int = 100,
        max_depth: int = 10,
        min_samples_leaf: int = 5,
        random_state: int = 42
    ):
        """
        Initialize the RandomForest model.

        Args:
            n_estimators: Number of trees in the forest
            max_depth: Maximum depth of each tree (None = unlimited)
            min_samples_leaf: Minimum samples required at leaf node
            random_state: Random seed for reproducibility
        """
        self.n_estimators = n_estimators
        self.max_depth = max_depth
        self.min_samples_leaf = min_samples_leaf
        self.random_state = random_state

        self.model = RandomForestClassifier(
            n_estimators=n_estimators,
            max_depth=max_depth,
            min_samples_leaf=min_samples_leaf,
            random_state=random_state,
            n_jobs=-1  # Use all CPU cores
        )

        self.feature_columns_: Optional[List[str]] = None
        self._is_fitted = False

    def fit(self, X, y) -> 'RandomForestModel':
        """
        Fit the RandomForest model.

        Args:
            X: Feature matrix (DataFrame or array-like)
            y: Target labels (0 or 1)

        Returns:
            self
        """
        # Store feature columns if DataFrame
        if hasattr(X, 'columns'):
            self.feature_columns_ = list(X.columns)
        elif isinstance(X, list):
            self.feature_columns_ = [f"feature_{i}" for i in range(len(X))]
        else:
            self.feature_columns_ = [f"feature_{i}" for i in range(X.shape[1])]

        self.model.fit(X, y)
        self._is_fitted = True
        return self

    def predict(self, X):
        """
        Predict class labels.

        Args:
            X: Feature matrix

        Returns:
            Array of predicted labels (0 or 1)
        """
        if not self._is_fitted:
            raise RuntimeError("Model has not been fitted. Call fit() first.")
        return self.model.predict(X)

    def predict_proba(self, X):
        """
        Predict class probabilities.

        Args:
            X: Feature matrix

        Returns:
            Array of shape (n_samples, 2) with [P(class=0), P(class=1)]
        """
        if not self._is_fitted:
            raise RuntimeError("Model has not been fitted. Call fit() first.")
        return self.model.predict_proba(X)

    def evaluate(self, X, y) -> ModelMetrics:
        """
        Evaluate the model and return metrics.

        Args:
            X: Feature matrix
            y: True labels

        Returns:
            ModelMetrics object with evaluation results
        """
        y_pred = self.predict(X)
        y_prob = self.predict_proba(X)[:, 1]

        return ModelMetrics(
            accuracy=accuracy_score(y, y_pred),
            precision=precision_score(y, y_pred, zero_division=0),
            recall=recall_score(y, y_pred, zero_division=0),
            f1=f1_score(y, y_pred, zero_division=0),
            log_loss=log_loss(y, y_prob),
            auc=roc_auc_score(y, y_prob)
        )

    def get_feature_importance(self) -> Dict[str, float]:
        """
        Get feature importance scores.

        Returns:
            Dictionary mapping feature names to importance scores
        """
        if not self._is_fitted:
            raise RuntimeError("Model has not been fitted. Call fit() first.")

        if self.feature_columns_ is None:
            return {}

        importances = self.model.feature_importances_
        return dict(zip(self.feature_columns_, importances))

    def get_top_features(self, n: int = 10) -> List[Tuple[str, float]]:
        """
        Get the top N most important features.

        Args:
            n: Number of top features to return

        Returns:
            List of (feature_name, importance) tuples, sorted by importance
        """
        importance = self.get_feature_importance()
        sorted_features = sorted(importance.items(), key=lambda x: x[1], reverse=True)
        return sorted_features[:n]

    def export_onnx(
        self,
        output_path: str,
        feature_columns: Optional[List[str]] = None
    ) -> str:
        """
        Export the model to ONNX format.

        Args:
            output_path: Path to save the ONNX model
            feature_columns: List of feature column names
                           (uses stored columns if not provided)

        Returns:
            Path to the saved ONNX model

        Raises:
            RuntimeError: If model has not been fitted
        """
        if not self._is_fitted:
            raise RuntimeError("Model has not been fitted. Call fit() first.")

        if feature_columns is None:
            feature_columns = self.feature_columns_

        if feature_columns is None:
            raise ValueError("Feature columns must be provided")

        n_features = len(feature_columns)

        # Define ONNX input type
        initial_type = [(self.ONNX_INPUT_NAME, FloatTensorType([None, n_features]))]

        # Convert to ONNX
        onnx_model = convert_sklearn(
            self.model,
            initial_types=initial_type,
            target_opset=self.TARGET_OPSET
        )

        # Save ONNX model
        output_path = Path(output_path)
        output_path.parent.mkdir(parents=True, exist_ok=True)

        with open(output_path, 'wb') as f:
            f.write(onnx_model.SerializeToString())

        print(f"✓ ONNX model saved: {output_path}")
        print(f"  Input: {self.ONNX_INPUT_NAME}, shape: [?, {n_features}]")
        print(f"  Outputs: {self.ONNX_OUTPUT_LABEL}, {self.ONNX_OUTPUT_PROBABILITY}")

        # Save feature columns JSON
        self._save_feature_info(output_path, feature_columns)

        return str(output_path)

    def _save_feature_info(self, onnx_path: Path, feature_columns: List[str]) -> str:
        """
        Save feature information alongside the ONNX model.

        Args:
            onnx_path: Path to the ONNX model
            feature_columns: List of feature column names

        Returns:
            Path to the saved feature info JSON
        """
        feature_info = {
            'model_name': self.MODEL_NAME,
            'feature_columns': feature_columns,
            'feature_count': len(feature_columns),
            'n_estimators': self.n_estimators,
            'max_depth': self.max_depth,
            'min_samples_leaf': self.min_samples_leaf
        }

        # Save as separate JSON file
        feature_info_path = str(onnx_path).replace('.onnx', '_features.json')
        with open(feature_info_path, 'w', encoding='utf-8') as f:
            json.dump(feature_info, f, indent=2)

        print(f"  Feature info saved: {feature_info_path}")
        return feature_info_path

    def save(self, path: str) -> str:
        """
        Save the model using joblib (native sklearn format).

        Args:
            path: Path to save the model

        Returns:
            Path to the saved model
        """
        path = Path(path)
        path.parent.mkdir(parents=True, exist_ok=True)

        joblib.dump({
            'model': self.model,
            'feature_columns': self.feature_columns_,
            'n_estimators': self.n_estimators,
            'max_depth': self.max_depth,
            'min_samples_leaf': self.min_samples_leaf
        }, path)

        print(f"✓ Model saved: {path}")
        return str(path)

    @classmethod
    def load(cls, path: str) -> 'RandomForestModel':
        """
        Load a model from disk.

        Args:
            path: Path to the saved model

        Returns:
            Loaded RandomForestModel instance
        """
        data = joblib.load(path)

        instance = cls(
            n_estimators=data['n_estimators'],
            max_depth=data['max_depth'],
            min_samples_leaf=data['min_samples_leaf']
        )
        instance.model = data['model']
        instance.feature_columns_ = data['feature_columns']
        instance._is_fitted = True

        print(f"✓ Model loaded: {path}")
        return instance


def create_model(
    n_estimators: int = 100,
    max_depth: int = 10,
    min_samples_leaf: int = 5,
    random_state: int = 42
) -> RandomForestModel:
    """
    Factory function to create a new RandomForest model.

    Args:
        n_estimators: Number of trees
        max_depth: Maximum tree depth
        min_samples_leaf: Minimum samples per leaf
        random_state: Random seed

    Returns:
        New RandomForestModel instance
    """
    return RandomForestModel(
        n_estimators=n_estimators,
        max_depth=max_depth,
        min_samples_leaf=min_samples_leaf,
        random_state=random_state
    )
