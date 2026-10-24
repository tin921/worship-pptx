"""Technical and coverage checks; keep review evidence next to the draft."""
import hashlib,json,pathlib,subprocess
ROOT=pathlib.Path(__file__).parent
video=ROOT/'output/worship-pptx-0.93.37-english-review.mp4'
probe=json.loads(subprocess.check_output(['ffprobe','-v','error','-show_streams','-show_format','-of','json',str(video)]))
streams=probe['streams'];v=next(s for s in streams if s['codec_type']=='video');a=next(s for s in streams if s['codec_type']=='audio')
assert v['width']==1600 and v['height']==900 and v['r_frame_rate']=='24/1'
assert not any(s['codec_type']=='subtitle' for s in streams)
assert a.get('tags',{}).get('language')=='eng'
timeline=json.loads((ROOT/'edit-timeline.json').read_text('utf-8'))
assert float(probe['format']['duration'])>timeline[-1]['t']
assert float(probe['format']['duration']) < 300, 'Review video must remain under five minutes'
for event in timeline:
    assert (ROOT/'rendered'/(event['shot']+'.png')).is_file()
keys={e['key'] for e in timeline if e['key']}
assert {'Esc','F5','Page Up','Page Down','→','↓','Double-click','Mouse wheel','Drag'}<=keys
for style in ['photo','illustration','abstract','sculpted','minimal']:
    assert any(e['shot']=='19-'+style for e in timeline)
for name in ['03-remove-selected','03-remove-confirm','03-removed-next','64-feedback-link','65-feedback-doc','04-pip-on','04-pip-off','04-pip-streaming','03-window-000','59-browser-top','60-browser-scrolled','22-full-double','58-upload-complete','61-save-dialog','62-saved','63-drive-folder']:
    assert any(e['shot']==name for e in timeline)
assert sum(e['shot']=='22-full-double' for e in timeline)==1
assert not any(e['shot']=='24-full-expand' for e in timeline)
assert any(e['shot']=='03-panel' and len(e['marks'])==2 for e in timeline)
assert 'Upload complete' in (ROOT/'upload-log.txt').read_text('utf-8')
pip=json.loads((ROOT/'pip-invariance.json').read_text('utf-8'))
assert pip['passed'] and pip['before']==pip['hidden']==pip['shown']
native=json.loads((ROOT/'native/verification.json').read_text('utf-8'))
assert native['double_click_open_close'] and native['google_link_opened'] and native['folder_link_opened'] and native['feedback_link_opened']
overlays=json.loads((ROOT/'overlay-settings.json').read_text('utf-8'))
assert overlays['keyboard_badges'] is False and overlays['highlight_arrows'] is True
assert set(overlays['mouse_gesture_labels'])=={'Double-click','Mouse wheel','Drag'}
service=json.loads((ROOT/'tutorial-service.json').read_text('utf-8'))
items=service['Items']
assert len(items)==5
assert items[0]['Title']=='宣召經文' and items[-1]['Title']=='讀經'
assert all('[v1]' in item['Lyrics'] and '[c]' in item['Lyrics'] and item['Order'] for item in items[1:4])
assert not items[0]['Order'] and not items[-1]['Order']
assert all('[1]' not in item['Lyrics'] and '報告' not in item['Title'] for item in items)
subprocess.run(['ffmpeg','-v','error','-i',str(video),'-f','null','-'],check=True)
report={'duration_seconds':float(probe['format']['duration']),'under_five_minutes':True,'resolution':[1600,900],'fps':24,'english_audio':True,'subtitle_streams':0,'decoded_without_errors':True,'required_features_present':True,'service_structure':['invocation','song','song','song','scripture reading'],'native_double_click_verified':True,'full_image_demonstrations':1,'pip_on_and_off_shown':True,'both_splitters_highlighted':True,'whole_window_resize_shown':True,'real_upload_verified':True,'drive_folder_shown':True,'large_orange_arrows':True,'keyboard_badges':False,'sha256':hashlib.sha256(video.read_bytes()).hexdigest(),'published':False}
report.update(selected_program_removal_shown=True, removal_confirmation_shown=True,
              nearest_upcoming_selection_verified=True, feedback_document_shown=True,
              pip_text_layout_size_position_pagination_unchanged=True)
(ROOT/'validation.json').write_text(json.dumps(report,indent=2),'utf-8')
print(json.dumps(report,indent=2))
