#!/usr/bin/env python3
"""Local interview control for a tool-enabled Codex/Work voice assistant. No AI calls."""
import argparse
import json
import sys
from urllib.request import Request, urlopen
from urllib.error import HTTPError


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--origin',default='http://localhost:8080',choices=['http://localhost:8080','http://127.0.0.1:8080'])
    parser.add_argument('command',choices=['context','begin','answer','ask','hint','next','turn','finish','summary'])
    parser.add_argument('session_id')
    parser.add_argument('--input-file',default='-',help='UTF-8 text or JSON file; - reads stdin. Never interpolate candidate text into shell code.')
    parser.add_argument('--request-id',help='Nonce returned by context/answer/finish; required for turn and summary')
    args=parser.parse_args()
    def api(path,data):
        req=Request(args.origin+'/api/mock/'+path,data=json.dumps(data,ensure_ascii=False).encode(),headers={'Content-Type':'application/json'})
        try:
            with urlopen(req,timeout=20) as response:return json.load(response)
        except HTTPError as error:
            try: message=json.loads(error.read(2000)).get('error',str(error))
            except (ValueError,AttributeError): message=str(error)
            raise ValueError(message) from None
    def read():
        if args.input_file=='-':return sys.stdin.read(250001)
        with open(args.input_file,encoding='utf-8') as source:return source.read(250001)
    if args.command=='begin':result=api('begin',{'session_id':args.session_id})
    elif args.command=='context':result=api('packet',{'session_id':args.session_id})
    elif args.command in ('answer','ask','hint','next','finish'):
        result=api('action',{'session_id':args.session_id,'action':args.command,'text':read() if args.command in ('answer','ask') else ''})
    else:
        if not args.request_id:parser.error('--request-id is required; retrieve current context first')
        result=api('complete',{'session_id':args.session_id,'request_id':args.request_id,'result':json.loads(read()),'provider':'codex-voice'})
    print(json.dumps(result,ensure_ascii=False))

if __name__=='__main__':
    try:main()
    except Exception as error:
        print(str(error),file=sys.stderr);sys.exit(1)
