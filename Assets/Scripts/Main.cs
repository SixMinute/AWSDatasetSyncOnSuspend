using UnityEngine;
using System.Collections;
using Amazon.CognitoIdentity;
using Amazon.Runtime;
using System;
using Amazon;
using Amazon.SecurityToken;
using System.Collections.Generic;
using Amazon.CognitoSync.SyncManager;
using Amazon.CognitoSync;
using System.Threading;

public class Main : MonoBehaviour
{
	public static readonly RegionEndpoint COGNITO_REGION = RegionEndpoint.USEast1;

	private CognitoAWSCredentials _credentials;
	public CognitoAWSCredentials credentials { get { return IfNull(
		ref this._credentials,
		() => new CognitoAWSCredentials(
			identityPoolId: _IdentityPoolId,
			cibClient: new AmazonCognitoIdentityClient(
				new AnonymousAWSCredentials(),
				new AmazonCognitoIdentityConfig { Timeout = TimeSpan.FromSeconds(5), RegionEndpoint = COGNITO_REGION }
			),
			stsClient: new AmazonSecurityTokenServiceClient(
				new AnonymousAWSCredentials(),
				new AmazonSecurityTokenServiceConfig { Timeout = TimeSpan.FromSeconds(5), RegionEndpoint =  COGNITO_REGION }
			),
			accountId: null, unAuthRoleArn: null, authRoleArn: null
		)
	); } }

	private CognitoSyncManager _syncManager;
	public CognitoSyncManager syncManager { get { return IfNull(
		ref this._syncManager,
		() => new CognitoSyncManager(
			credentials,
			new AmazonCognitoSyncConfig { Timeout = TimeSpan.FromSeconds(5), RegionEndpoint = COGNITO_REGION }
		)
	); } }

	private Dataset _dataset;
	public Dataset dataset { get { return IfNull(
		ref this._dataset,
		() => syncManager.OpenOrCreateDataset("myDataset")
	); } }
	
	public string _IdentityPoolId;
	private bool _IsSetup = false;
	
	public static T IfNull<T>(ref T value, Func<T> lambda, bool notDefault=false)
	{
		if( (null == value) || ( notDefault && EqualityComparer<T>.Default.Equals(default(T), value) ) )
		{
			value = lambda.Invoke();
		}
		return value;
	}

	void Start ()
	{
		credentials.GetCredentialsAsync( result => {
			if(null != result.Exception)
			{
				Debug.Log(result.Exception);
				return;
			}
			Debug.Log(result.Response);
			Debug.Log( credentials.GetIdentityId() );
			_IsSetup = true;
		} );
	}
	
    public void OnGUI()
    {
        if (GUI.Button(new Rect(15, 15, 300, 100), "Sync synchronize"))
        {
            SynchronizeAndWait();
        }
    }
	
	public void OnApplicationPause(bool isSuspended)
	{
		if(!_IsSetup)
		{
			return;
		}
        if (!isSuspended)
		{
            SynchronizeAndWait();
        }
	}

    private void SynchronizeAndWait()
    {
        ManualResetEvent waitLock = new ManualResetEvent(false);

        EventHandler<SyncSuccessEvent> afterSync = null;
        EventHandler<SyncFailureEvent> afterFail = null;

        Action unsubscribe = () => {
            waitLock.Set();
            dataset.OnSyncSuccess -= afterSync;
            dataset.OnSyncFailure -= afterFail;
        };

        afterSync = (object sender, SyncSuccessEvent e) => {
            unsubscribe.Invoke();
        };
        afterFail = (object sender, SyncFailureEvent e) => {
            unsubscribe.Invoke();
        };

        dataset.OnSyncSuccess += afterSync;
        dataset.OnSyncFailure += afterFail;

        Debug.Log("Synchronizing");
        new System.Threading.Thread(() => {
            dataset.Synchronize();
            Debug.Log("Waiting");
            waitLock.WaitOne();
            Debug.Log("Synchronized");
        }).Start();

    }

}
