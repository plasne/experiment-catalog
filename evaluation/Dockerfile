# create from base
FROM python:3.12.3-bookworm@sha256:76831bb094456f98613115be37cc8d4f9ab0d128467726ed3160044c80546437
WORKDIR /app

# copy
COPY coherence.txt coherence.txt
COPY eval.py eval.py
COPY groundedness.txt groundedness.txt
COPY relevance.txt relevance.txt
COPY requirements.txt requirements.txt

# install requirements
RUN pip install --no-cache-dir -r requirements.txt

# run
EXPOSE 80
CMD ["python", "eval.py"]