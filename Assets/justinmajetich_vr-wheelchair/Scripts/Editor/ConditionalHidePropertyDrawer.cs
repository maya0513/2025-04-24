using UnityEngine;
using UnityEditor;

//ConditionalHideAttributeの元バージョンはBrecht Lecluyse (www.brechtos.com)によって作成
//修正者: Justin Majetich

[CustomPropertyDrawer(typeof(ConditionalHideAttribute))]
public class ConditionalHidePropertyDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {

        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        bool wasEnabled = GUI.enabled;
        GUI.enabled = enabled;
        if (!condHAtt.HideInInspector || enabled)
        {
            EditorGUI.PropertyField(position, property, label, true);
        }

        GUI.enabled = wasEnabled;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ConditionalHideAttribute condHAtt = (ConditionalHideAttribute)attribute;
        bool enabled = GetConditionalHideAttributeResult(condHAtt, property);

        if (!condHAtt.HideInInspector || enabled)
        {
            return EditorGUI.GetPropertyHeight(property, label);
        }
        else
        {
            //プロパティが描画されていない場合
            //プロパティの前後に追加されたスペースを元に戻したい
            return -EditorGUIUtility.standardVerticalSpacing;
            //return 0.0f;
        }


        /*
        //展開されていない時のベースの高さを取得
        var height = base.GetPropertyHeight(property, label);

        // プロパティが展開されている場合、すべての子要素を通して高さを取得
        if (property.isExpanded)
        {
            var propEnum = property.GetEnumerator();
            while (propEnum.MoveNext())
                height += EditorGUI.GetPropertyHeight((SerializedProperty)propEnum.Current, GUIContent.none, true);
        }
        return height;*/
    }

    private bool GetConditionalHideAttributeResult(ConditionalHideAttribute condHAtt, SerializedProperty property)
    {
        bool enabled = (condHAtt.UseOrLogic) ? false : true;

        //プライマリプロパティを処理
        SerializedProperty sourcePropertyValue = null;
        //ソースフィールドの完全な相対プロパティパスを取得して、ネストした隠蔽ができるようにする。配列を扱う時は古いメソッドを使用
        if (!property.isArray)
        {
            string propertyPath = property.propertyPath; //属性を適用したいプロパティのプロパティパスを返す
            string conditionPath = propertyPath.Replace(property.name, condHAtt.ConditionalSourceField); //パスを条件ソースプロパティのパスに変更
            sourcePropertyValue = property.serializedObject.FindProperty(conditionPath);

            //検索に失敗した場合->古いシステムにフォールバック
            if (sourcePropertyValue == null)
            {
                //元の実装（ネストしたシリアライズオブジェクトでは動作しない）
                sourcePropertyValue = property.serializedObject.FindProperty(condHAtt.ConditionalSourceField);
            }
        }
        else
        {
            //元の実装（ネストしたシリアライズオブジェクトでは動作しない）
            sourcePropertyValue = property.serializedObject.FindProperty(condHAtt.ConditionalSourceField);
        }


        if (sourcePropertyValue != null)
        {
            enabled = CheckPropertyType(sourcePropertyValue);
            if (condHAtt.InverseCondition1) enabled = !enabled;
        }
        else
        {
            //Debug.LogWarning("ConditionalHideAttributeを使用しようとしていますが、オブジェクト内で一致するSourcePropertyValueが見つかりません: " + condHAtt.ConditionalSourceField);
        }


        //セカンダリプロパティを処理
        SerializedProperty sourcePropertyValue2 = null;
        if (!property.isArray)
        {
            string propertyPath = property.propertyPath; //属性を適用したいプロパティのプロパティパスを返す
            string conditionPath = propertyPath.Replace(property.name, condHAtt.ConditionalSourceField2); //パスを条件ソースプロパティのパスに変更
            sourcePropertyValue2 = property.serializedObject.FindProperty(conditionPath);

            //検索に失敗した場合->古いシステムにフォールバック
            if (sourcePropertyValue2 == null)
            {
                //元の実装（ネストしたシリアライズオブジェクトでは動作しない）
                sourcePropertyValue2 = property.serializedObject.FindProperty(condHAtt.ConditionalSourceField2);
            }
        }
        else
        {
            // 元の実装（ネストしたシリアライズオブジェクトでは動作しない）
            sourcePropertyValue2 = property.serializedObject.FindProperty(condHAtt.ConditionalSourceField2);
        }

        //結果を結合
        if (sourcePropertyValue2 != null)
        {
            bool prop2Enabled = CheckPropertyType(sourcePropertyValue2);
            if (condHAtt.InverseCondition2) prop2Enabled = !prop2Enabled;

            if (condHAtt.UseOrLogic)
                enabled = enabled || prop2Enabled;
            else
                enabled = enabled && prop2Enabled;
        }
        else
        {
            //Debug.LogWarning("ConditionalHideAttributeを使用しようとしていますが、オブジェクト内で一致するSourcePropertyValueが見つかりません: " + condHAtt.ConditionalSourceField);
        }

        //無制限のプロパティ配列を処理
        string[] conditionalSourceFieldArray = condHAtt.ConditionalSourceFields;
        bool[] conditionalSourceFieldInverseArray = condHAtt.ConditionalSourceFieldInverseBools;
        for (int index = 0; index < conditionalSourceFieldArray.Length; ++index)
        {
            SerializedProperty sourcePropertyValueFromArray = null;
            if (!property.isArray)
            {
                string propertyPath = property.propertyPath; //属性を適用したいプロパティのプロパティパスを返す
                string conditionPath = propertyPath.Replace(property.name, conditionalSourceFieldArray[index]); //パスを条件ソースプロパティのパスに変更
                sourcePropertyValueFromArray = property.serializedObject.FindProperty(conditionPath);

                //検索に失敗した場合->古いシステムにフォールバック
                if (sourcePropertyValueFromArray == null)
                {
                    //元の実装（ネストしたシリアライズオブジェクトでは動作しない）
                    sourcePropertyValueFromArray = property.serializedObject.FindProperty(conditionalSourceFieldArray[index]);
                }
            }
            else
            {
                // 元の実装（ネストしたシリアライズオブジェクトでは動作しない）
                sourcePropertyValueFromArray = property.serializedObject.FindProperty(conditionalSourceFieldArray[index]);
            }

            //結果を結合
            if (sourcePropertyValueFromArray != null)
            {
                bool propertyEnabled = CheckPropertyType(sourcePropertyValueFromArray);
                if (conditionalSourceFieldInverseArray.Length >= (index + 1) && conditionalSourceFieldInverseArray[index]) propertyEnabled = !propertyEnabled;

                if (condHAtt.UseOrLogic)
                    enabled = enabled || propertyEnabled;
                else
                    enabled = enabled && propertyEnabled;
            }
            else
            {
                //Debug.LogWarning("ConditionalHideAttributeを使用しようとしていますが、オブジェクト内で一致するSourcePropertyValueが見つかりません: " + condHAtt.ConditionalSourceField);
            }
        }


        //すべてをまとめる
        if (condHAtt.Inverse) enabled = !enabled;

        return enabled;
    }

    private bool CheckPropertyType(SerializedProperty sourcePropertyValue)
    {
        //注意: 必要に応じて他の型のカスタム処理を追加
        switch (sourcePropertyValue.propertyType)
        {
            case SerializedPropertyType.Boolean:
                return sourcePropertyValue.boolValue;
            case SerializedPropertyType.ObjectReference:
                return sourcePropertyValue.objectReferenceValue != null;
            default:
                Debug.LogError("条件付き隠蔽に使用されたプロパティのデータ型 [" + sourcePropertyValue.propertyType + "] は現在サポートされていません");
                return true;
        }
    }
}