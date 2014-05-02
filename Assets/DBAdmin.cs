using Jboy;
using UnityEngine;
using uGameDB;
using System.Collections;
using System.Collections.Generic;

public class DBAdmin : MonoBehaviour {
	public Font font;
	public int JsonIndentSize = 20;
	public GUIStyle bucketNameStyle;
	public GUIStyle propertyStyle;
	public GUIStyle valueStyle;
	public GUIStyle separatorStyle;
	
	public class DBValue {
		public string text;
		public JsonTree tree;
		public Encoding encoding;
		public bool expanded;
	}
	
	private List<Bucket> _buckets;
	private Bucket currentBucket;
	private bool _enableRefresh = true;
	private Dictionary<string, DBValue> data;
	private Vector2 keysScrollPosition;
	
	private string deleteKey = "";
	private string editKey = "";
	private DBValue editValue = null;
	private Bucket clearBucket = null;

	private Vector2 bucketsScrollPosition;
	
	// Start
	void Start () {
		// Reduce CPU usage
		Application.targetFrameRate = 25;
		
		_buckets = new List<Bucket>();
		data = new Dictionary<string, DBValue>();
		
		// Refresh the list once on start.
		StartCoroutine(RefreshList());
	}

	// OnGUI
	void OnGUI() {
		if(GUI.skin.font != font) {
			GUI.skin.font = font;
		}
		
		// Left column
		GUILayout.BeginArea(new Rect(0, 0, Screen.width * 0.25f, Screen.height));
		GUILayout.BeginVertical("box");
		
		bucketsScrollPosition = GUILayout.BeginScrollView(bucketsScrollPosition);
		foreach(var bucket in _buckets) {
			GUILayout.BeginHorizontal();
			
			if(bucket == currentBucket) {
				GUI.contentColor = Color.green;
			} else {
				GUI.contentColor = Color.white;
			}
			
			if(GUILayout.Button(bucket.name, bucketNameStyle)) {
				StartCoroutine(GetKeysAndValues(bucket));
			}
			GUILayout.EndHorizontal();
		}
		GUI.contentColor = Color.white;
		
		GUILayout.FlexibleSpace();
		GUILayout.EndScrollView();
		
		// Footer
		GUILayout.BeginHorizontal();
		//GUILayout.Label(currentBucket.name, bucketNameStyle);
		GUILayout.FlexibleSpace();
		
		GUI.enabled = _enableRefresh;
		if(GUILayout.Button("Refresh")) {
			StartCoroutine(RefreshList());
		}
		
		GUI.enabled = true;
		if(currentBucket != null && GUILayout.Button("Clear bucket")) {
			clearBucket = currentBucket;
		}
		
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		
		GUILayout.EndVertical();
		GUILayout.EndArea();
		
		// Right column
		GUILayout.BeginArea(new Rect(Screen.width * 0.25f, 0, Screen.width * 0.75f, Screen.height));
		GUILayout.BeginVertical("box");
		
		keysScrollPosition = GUILayout.BeginScrollView(keysScrollPosition);
		foreach(var entry in data) {
			GUILayout.BeginHorizontal();
			GUILayout.Label(entry.Key, GUILayout.Width(Screen.width / 7.0f));
			
			if(entry.Value != null) {
				if(entry.Value.encoding == Encoding.Json && GUILayout.Button(entry.Value.expanded ? "-" : "+")) {
					entry.Value.expanded = !entry.Value.expanded;
				}
				
				if(entry.Value.expanded) {
					DrawJsonTreeExpanded(entry.Value.tree);
				} else {
					GUILayout.Label(entry.Value.text);
					GUILayout.FlexibleSpace();
				}
				
				if(entry.Value.tree != null) {
					if(entry.Value.tree.IsString) {
						GUILayout.Label(entry.Value.tree.AsString.ToString());
					}
				}
				
				GUILayout.Label(entry.Value.encoding.ToString());
				
				// Toolbar
				if(entry.Value.encoding == Encoding.Bitstream && GUILayout.Button("Edit")) {
					editKey = entry.Key;
					editValue = entry.Value;
				}
			} else {
				GUILayout.FlexibleSpace();
			}
			
			if(GUILayout.Button("Remove")) {
				deleteKey = entry.Key;
			}
			
			GUILayout.EndHorizontal();
		}
		GUILayout.FlexibleSpace();
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
		GUILayout.EndArea();
		
		if(deleteKey != "") {
			GUI.backgroundColor = Color.red;
			GUILayout.Window(0, new Rect(Screen.width / 2 - Screen.width / 4, Screen.height / 2 - 50, Screen.width / 2, 100), DeleteWindow, "Are you sure you want to delete '" + deleteKey + "'?");
		} else if (editKey != "") {
			GUI.backgroundColor = Color.green;
			GUILayout.Window(1, new Rect(Screen.width / 2 - Screen.width / 4, Screen.height / 2 - Screen.height / 4, Screen.width / 2, Screen.height / 2), EditWindow, editKey);
		} else if(clearBucket != null) {
			GUI.backgroundColor = Color.red;
			GUILayout.Window(0, new Rect(Screen.width / 2 - Screen.width / 4, Screen.height / 2 - 50, Screen.width / 2, 100), DeleteBucketWindow, "Are you sure you want to delete '" + clearBucket.name + "'?");
		}
	}

	// EditWindow
	void EditWindow(int id) {
		//GUILayout.FlexibleSpace();
		editValue.text = GUILayout.TextArea(editValue.text);
		
		GUILayout.FlexibleSpace();
		if(GUILayout.Button("Update")) {
			StartCoroutine(SetKeyValue(currentBucket, editKey, editValue));
		}
		if(GUILayout.Button("Cancel")) {
			editKey = "";
			editValue = null;
		}
	}

	// DeleteWindow
	void DeleteWindow(int id) {
		GUILayout.FlexibleSpace();
		if(GUILayout.Button("Yes")) {
			StartCoroutine(Remove(currentBucket, deleteKey));
			deleteKey = "";
		}
		if(GUILayout.Button("No")) {
			deleteKey = "";
		}
		GUILayout.FlexibleSpace();
	}

	// DeleteBucketWindow
	void DeleteBucketWindow(int id) {
		GUILayout.FlexibleSpace();
		if(GUILayout.Button("Yes")) {
			_buckets.Remove(currentBucket);
			StartCoroutine(ClearBucket(currentBucket));
			clearBucket = null;
			currentBucket = null;
		}
		if(GUILayout.Button("No")) {
			clearBucket = null;
		}
		GUILayout.FlexibleSpace();
	}

	// DrawJsonTreeExpanded
	void DrawJsonTreeExpanded(JsonTree jsonTree) {
		if(jsonTree.IsObject) {
			GUILayout.BeginVertical();
			GUILayout.Label("{", separatorStyle);
			
			foreach (var prop in jsonTree.AsObject) {
				GUILayout.BeginHorizontal();
				GUILayout.Space(JsonIndentSize);
				GUILayout.Label(prop.Name, propertyStyle);
				GUILayout.Label(":", separatorStyle);
				DrawJsonTreeExpanded(prop.Value);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			
			GUILayout.Label("}", separatorStyle);
			GUILayout.EndVertical();
		} else if (jsonTree.IsArray) {
			GUILayout.BeginVertical();
			GUILayout.Label("[", separatorStyle);
			
			foreach (var item in jsonTree.AsArray) {
				GUILayout.BeginHorizontal();
				GUILayout.Space(JsonIndentSize);
				DrawJsonTreeExpanded(item);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}
			
			GUILayout.Label("]", separatorStyle);
			GUILayout.EndVertical();
		} else {
			GUILayout.BeginHorizontal();
			//GUILayout.TextField(jsonTree.ToString(), "Label");
			GUILayout.Label(jsonTree.ToString(), valueStyle);
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
		}
	}

	// SetKeyValue
	IEnumerator SetKeyValue(Bucket bucket, string key, DBValue val) {
		bool success = false;
		
		// Get the value for the key
		switch(val.encoding) {
		case Encoding.Bitstream:
			var setBSReq = bucket.Set(key, val.text, Encoding.Bitstream);
			yield return setBSReq.WaitUntilDone();
			success = setBSReq.isSuccessful;
			break;
			
		case Encoding.Json:
			var reader = new JsonReader(val.text);
			reader.ReadObjectStart();
			reader.ReadPropertyName("v");
			var valJson = reader.ReadString();
			reader.ReadObjectEnd();
			
			var setJSONReq = bucket.Set(key, valJson, Encoding.Json);
			yield return setJSONReq.WaitUntilDone();
			success = setJSONReq.isSuccessful;
			break;
		}
		
		if(success) {
			editKey = "";
			editValue = null;
			
			StartCoroutine(GetKeyValue(bucket, key));
		} else {
			Debug.LogError("Couldn't update key '" + key + "' to value '" + val.text + "'");
		}
	}

	// GetKeysAndValues
	IEnumerator GetKeysAndValues(Bucket bucket) {
		var getKeysReq = bucket.GetKeys();
		yield return getKeysReq.WaitUntilDone();
		
		if(!getKeysReq.isSuccessful) {
			Debug.LogError("Error querying keys for " + bucket.name);
			yield break;
		}
		
		keysScrollPosition = Vector2.zero;
		data = new Dictionary<string, DBValue>();
		foreach(var key in getKeysReq.GetKeyEnumerable()) {
			data[key] = null;
			StartCoroutine(GetKeyValue(bucket, key));
		}
		
		currentBucket = bucket;
		Debug.Log("Active bucket: " + bucket.name);
	}

	// GetKeyValue
	IEnumerator GetKeyValue(Bucket bucket, string key) {
		// Get the value for the key
		var getReq = bucket.Get(key);
		yield return getReq.WaitUntilDone();

		if(getReq.isSuccessful) {
			// Try to deserialize the data to a string. If this does not work, use the raw data.
			var encoding = getReq.GetEncoding();
			if(encoding == Encoding.Json) {
				var tree = getReq.GetValue<JsonTree>();
				data[key] = new DBValue {
					text = tree.ToString(),
					tree = tree,
					encoding = encoding
				};
			} else {
				data[key] = new DBValue {
					text = getReq.GetValueRaw(),
					tree = null,
					encoding = encoding
				};
			}
		} else {
			Debug.LogError("Error querying value for key: " + key);
		}
	}

	// Remove
	IEnumerator Remove(Bucket bucket, string key) {
		// Remove one entry from the database.
		var removeRequest = bucket.Remove(key);
		yield return removeRequest.WaitUntilDone();
		
		if (removeRequest.isSuccessful) {
			data.Remove(key);
		} else {
			Debug.LogError("Unable to remove '" + removeRequest.key + "' from " + removeRequest.bucket + ". " + removeRequest.GetErrorString());
		} 
	}
	
	IEnumerator ClearBucket(Bucket bucket) {
		// Get all keys in the given bucket.
		var getKeysRequest = bucket.GetKeys();
		yield return getKeysRequest.WaitUntilDone();
		if (getKeysRequest.hasFailed) {
			Debug.LogError("Unable to clear " + bucket + ". " + getKeysRequest.GetErrorString());
			yield break;
		}

		// Remove each one of the keys. Issue the requests all at once.
		var removeRequests = new List<RemoveRequest>();
		foreach(var key in getKeysRequest.GetKeyEnumerable()) {
			removeRequests.Add(bucket.Remove(key));
		}

		// Then wait for each request to finish.
		foreach (var removeRequest in removeRequests) {
			yield return removeRequest.WaitUntilDone();
			if (removeRequest.hasFailed) {
				Debug.LogError("Unable to remove '" + removeRequest.key + "' from " + removeRequest.bucket + ". " + removeRequest.GetErrorString());
			}
		}

		// Finally refresh the list.
		StartCoroutine(RefreshList());
	}
		
	IEnumerator RefreshList() {
		// Only allow a new refresh if the previous one has finished.
		if (!_enableRefresh) yield break;
		_enableRefresh = false;

		// If the uGameDB is not connected to Riak, wait and try again.
		while(!Database.isConnected)
			yield return new WaitForSeconds(0.5f);

		// Refresh the buckets and wait for finish.
		yield return StartCoroutine(RefreshBuckets());

		// We are done, a new refresh can be started.
		_enableRefresh = true;
	}
	
	IEnumerator RefreshBuckets() {
		// Build a separate bucket list until the operation finishes.
		var newBuckets = new List<Bucket>();

		// Get a list of all buckets
		var getBucketsReq = Bucket.GetBuckets();
		yield return getBucketsReq.WaitUntilDone();
		if (getBucketsReq.isSuccessful) {
			foreach(var bucket in getBucketsReq.GetBucketEnumerable()) {
				newBuckets.Add(bucket);
			}
			
			// Sort the buckets by their name.
			newBuckets.Sort((a, b) => a.name.CompareTo(b.name));
		}
		
		_buckets = newBuckets;
	}
}
