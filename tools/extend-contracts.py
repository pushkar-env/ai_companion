"""Initial executable resource/event scaffolds for later milestones; no service implementation implied."""
import json,pathlib
r=pathlib.Path(__file__).resolve().parents[1];out=r/'packages/contracts'
def s(lo=0,hi=16000):return {'type':'string','minLength':lo,'maxLength':hi}
def enum(*v):return {'type':'string','enum':list(v)}
def ref(name):return {'$ref':'#/$defs/'+name}
def array(item,maxItems=100):return {'type':'array','items':item,'maxItems':maxItems}
def obj(props,required=None,write=False):return {'type':'object','properties':props,'required':list(props) if required is None else required,**({'additionalProperties':False} if write else {})}
uuid={'type':'string','format':'uuid'};dt={'type':'string','format':'date-time'};integer={'type':'integer','minimum':0};unit={'type':'number','minimum':0,'maximum':1};boolean={'type':'boolean'}
nullable=lambda t:{'anyOf':[t,{'type':'null'}]}
common={'id':uuid,'version':{'type':'integer','minimum':1},'created_at':dt,'updated_at':dt}
resource=lambda p:obj({**common,**p})
d={}
d['Problem']=obj({'type':s(1,300),'title':s(1,160),'status':{'type':'integer','minimum':400,'maximum':599},'code':s(1,80),'detail':s(0,1000),'trace_id':s(1,128),'retryable':boolean,'field_errors':{'type':'object','additionalProperties':s(0,300)}})
d['Traits']=obj({n:unit for n in ['warmth','humor','energy','curiosity','directness']},write=True)
d['UserProfile']=resource({'display_name':s(1,60),'locale':s(2,20),'account_status':s(1,40),'consent_versions':{'type':'object','additionalProperties':s(1,100)}})
d['ConsentRecord']=resource({'purpose':s(1,80),'granted':boolean,'policy_version':s(1,100)})
d['Companion']=resource({'definition_id':uuid,'definition_version':integer,'name':s(1,40),'pronouns':s(1,40),'traits':ref('Traits'),'voice_profile_id':s(1,100),'rig_family':s(1,100),'loadout_version':integer})
d['Conversation']=resource({'companion_id':uuid,'title':s(0,120),'state':enum('active','deleting'),'latest_turn_at':nullable(dt)})
d['Message']=resource({'conversation_id':uuid,'turn_id':uuid,'role':enum('user','assistant'),'text':s(0,16000),'status':enum('accepted','streaming','completed','failed','cancelled','interrupted'),'source':enum('text','voice'),'client_message_id':nullable(uuid),'heard_duration_ms':nullable(integer),'sequence':integer})
d['Memory']=resource({'companion_id':uuid,'text':s(1,1000),'type':enum('preference','fact','explicit_note'),'source_message_ids':array(uuid),'confidence':unit,'status':s(1,40),'expires_at':nullable(dt),'editable':boolean})
d['InventoryItem']=resource({'catalog_item_id':uuid,'source':enum('purchase','subscription','free','grant'),'entitlement_id':nullable(s(1,100)),'state':enum('active','revoked','expired')})
d['Entitlement']=obj({'entitlement_id':s(1,100),'state':enum('active','revoked','expired'),'expires_at':nullable(dt)})
d['EntitlementSnapshot']=obj({'version':integer,'fetched_at':dt,'valid_until':dt,'items':array(ref('Entitlement'))})
d['OperationStatus']=obj({'id':uuid,'type':s(1,80),'state':enum('pending','running','completed','failed'),'requested_at':dt,'completed_at':nullable(dt),'safe_error':nullable(s(0,300)),'result_url':nullable(s(1,2048))})
d['AudioConfig']=obj({'sample_rate':{'type':'integer','enum':[16000,24000,48000]},'channels':{'const':1},'codec':s(1,40)})
d['VoiceSession']=obj({'id':uuid,'transport':enum('livekit','mock','approved_direct'),'server_url':s(1,2048),'join_credential':s(1,8192),'credential_expires_at':dt,'lease_expires_at':dt,'session_deadline':dt,'room_id':s(1,128),'playback_epoch':integer,'approved_audio_config':ref('AudioConfig'),'remaining_seconds':integer})
d['VoiceSummary']=obj({'session_id':uuid,'state':enum('ended','failed'),'heard_ms':integer})
d['CatalogItem']=resource({'item_type':s(1,80),'rig_compatibility':array(s(1,100)),'slots':array(s(1,60)),'status':s(1,40)})
d['Loadout']=resource({'companion_id':uuid,'slot_items':{'type':'object','additionalProperties':uuid}})
d['Device']=resource({'platform':enum('android','ios'),'locale':s(2,20),'timezone':s(1,80)})
d['NotificationPreferences']=resource({'categories':{'type':'object','additionalProperties':boolean},'quiet_hours':obj({'start':s(5,5),'end':s(5,5),'timezone':s(1,80)})})
d['Report']=resource({'message_id':nullable(uuid),'session_id':nullable(uuid),'category':s(1,80),'details':s(0,2000)})
d['ClientConfig']=obj({'schema_version':{'const':1},'environment':enum('local','development','staging','production'),'mock':boolean,'realtime_voice_enabled':boolean,'memory_enabled':boolean,'purchases_enabled':boolean,'new_catalog_enabled':boolean,'provider_route_version':s(1,80),'persona_version':s(1,80),'maximum_session_seconds':integer,'emergency_generation_disabled':boolean})
d['Expression']=obj({'schema_version':{'const':1},'expression':enum('neutral','warm','curious','thoughtful','concerned','celebratory'),'valence':{'type':'number','minimum':-1,'maximum':1},'arousal':unit,'intensity':unit,'gesture':enum('none','small_nod','small_shake','open_hand'),'duration_ms':{'type':'integer','minimum':100,'maximum':5000}})
visemes='sil PP FF TH DD kk CH SS nn RR aa E ih oh ou'.split()
d['VisemeFrame']=obj({'offset_ms':integer,'duration_ms':{'type':'integer','minimum':1,'maximum':500},'weights':obj({n:unit for n in visemes},[],True)})
payloads={
 'voice.state':obj({'session_id':uuid,'state':enum('idle','requesting_permission','authorizing','connecting','listening','user_speaking','thinking','agent_speaking','reconnecting','ending','ended','failed'),'playback_epoch':integer}),
 'voice.transcript':obj({'utterance_id':uuid,'role':enum('user','assistant'),'text':s(0,16000),'is_final':boolean,'playback_epoch':integer}),
 'voice.playout':obj({'utterance_id':uuid,'played_ms':integer,'playback_epoch':integer}),
 'voice.interrupted':obj({'utterance_id':uuid,'heard_ms':integer,'playback_epoch':integer}),
 'avatar.visemes':obj({'utterance_id':uuid,'playback_epoch':integer,'frames':array(ref('VisemeFrame'),200)}),
 'entitlement.changed':obj({'snapshot_version':integer}),
 'memory.invalidated':obj({'memory_epoch':integer})}
internal={
 'message.completed':{'message_id':uuid,'consent_epoch':integer},
 'memory.candidate.created':{'candidate_id':uuid},'memory.deleted':{'memory_id':uuid,'memory_epoch':integer},
 'purchase.verified':{'provider_event_id':s(1,200),'transaction_id':s(1,200)},
 'entitlement.updated':{'user_id':uuid,'snapshot_version':integer},'asset.catalog.published':{'catalog_version':integer,'platform':enum('android','ios')},
 'privacy.deletion.requested':{'deletion_id':uuid,'user_id':uuid},'usage.finalized':{'reservation_id':uuid,'normalized_units':integer},'report.created':{'report_id':uuid}}
envelope={'event_id':uuid,'schema_version':{'const':1},'type':s(1,100),'aggregate_id':uuid,'seq':integer,'occurred_at':dt,'trace_id':s(1,128),'payload':{'type':'object'}}
for name,p in payloads.items():d[''.join(x.title() for x in name.split('.'))+'Payload']=p
d['SupplementalClientEvent']={**obj(envelope),'oneOf':[{'properties':{'type':{'const':n},'payload':p}} for n,p in payloads.items()]}
d['InternalEvent']={**obj({**envelope,'owner_id':uuid}),'oneOf':[{'properties':{'type':{'const':n},'payload':obj(p,write=True)}} for n,p in internal.items()]}
(out/'resources.schema.json').write_text(json.dumps({'$schema':'https://json-schema.org/draft/2020-12/schema','$id':'urn:companion:resources:1','$defs':d},indent=2)+'\n')
print('Wrote typed resource and event scaffolds for later milestones (not implemented routes).')
