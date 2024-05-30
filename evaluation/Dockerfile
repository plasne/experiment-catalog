# create from base
FROM python:3.12.3-bookworm
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